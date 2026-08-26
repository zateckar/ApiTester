using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ApiTester
{
    /// <summary>
    /// Overrides the stored column name for a property, so a C# rename does not
    /// orphan the existing column in databases already out in the wild.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class ColumnNameAttribute : Attribute
    {
        public ColumnNameAttribute(string name) => Name = name;
        public string Name { get; }
    }

    /// <summary>
    /// Backfill a stored value for rows where a newly added column is still NULL. Used when
    /// the model property's default is not what SQLite's "alter table add column" leaves
    /// behind - NULL, which reads as false for a bool, or 0 / empty for the rest.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class BackfillAttribute : Attribute
    {
        public BackfillAttribute(object value) => Value = value;
        public object Value { get; }
    }

    /// <summary>
    /// Reflection based table description. Deliberately minimal - just enough to map the
    /// handful of plain property-bag models this app persists.
    /// </summary>
    internal sealed class TableMap
    {
        //Annotation matters under Native AOT: without it the trimmer drops the models' property
        //metadata, GetProperties returns nothing, and the generated SQL degenerates to
        //"create table "Setting" ()" - which fails with 'near ")": syntax error'.
        internal const DynamicallyAccessedMemberTypes MappedMembers =
            DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;

        private static readonly Dictionary<Type, TableMap> Cache = new();

        public string TableName { get; }
        public PropertyInfo Key { get; }
        public string KeyColumn { get; }
        public IReadOnlyList<PropertyInfo> Columns { get; }

        private TableMap([DynamicallyAccessedMembers(MappedMembers)] Type type)
        {
            TableName = type.Name;

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead && p.CanWrite && SqlType(p.PropertyType) is not null)
                            .ToList();

            Key = props.FirstOrDefault(p => p.Name == "Id" && p.PropertyType == typeof(int));
            KeyColumn = Key is null ? null : ColumnOf(Key);
            Columns = props;
        }

        public static TableMap For([DynamicallyAccessedMembers(MappedMembers)] Type type)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(type, out var map))
                {
                    map = new TableMap(type);
                    Cache[type] = map;
                }
                return map;
            }
        }

        public static string ColumnOf(PropertyInfo p)
            => p.GetCustomAttribute<ColumnNameAttribute>()?.Name ?? p.Name;

        /// <summary>
        /// Declared types match what sqlite-net used to emit, so existing databases keep
        /// exactly the same schema text and nothing looks like a migration.
        /// </summary>
        public static string SqlType(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;

            if (t == typeof(string)) return "varchar";
            if (t == typeof(int) || t == typeof(bool)) return "integer";
            if (t == typeof(long)) return "bigint";
            if (t == typeof(double) || t == typeof(float)) return "float";
            if (t == typeof(byte[])) return "blob";

            return null;
        }
    }

    /// <summary>
    /// Thin async wrapper over Microsoft.Data.Sqlite. Replaces the sqlite-net ORM with
    /// plain parameterised SQL while keeping the same call shape the forms already use.
    /// </summary>
    internal sealed class SqliteStore : IAsyncDisposable
    {
        private readonly string connectionString;
        private SqliteConnection connection;

        //One connection, two callers: the forms and the background sync, both on the UI thread
        //but interleaving at every await. Each public operation holds this for its whole run, so
        //a command can never start while another one's reader is still open. Only the public
        //surface takes it - the private helpers run inside a call that already holds it.
        private readonly SemaphoreSlim gate = new(1, 1);

        public string DatabasePath { get; }

        public SqliteStore(string databasePath)
        {
            //Resolve to a full path now: the connection opens lazily, possibly after a file
            //dialog has moved Environment.CurrentDirectory, and a relative path would then
            //silently open (or create) a different database file than the profile names.
            DatabasePath = Path.GetFullPath(databasePath);

            //Pooling off so CloseAsync really releases the file - the export copies the
            //database on disk straight afterwards.
            connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Pooling = false
            }.ToString();
        }

        private async Task<SqliteConnection> Open()
        {
            if (connection is null)
            {
                connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();
            }

            return connection;
        }

        public async Task CloseAsync()
        {
            await gate.WaitAsync();

            try
            {
                if (connection is null) return;

                await connection.CloseAsync();
                await connection.DisposeAsync();
                connection = null;
            }
            finally { gate.Release(); }
        }

        public ValueTask DisposeAsync() => new(CloseAsync());

        private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

        /// <summary>
        /// Creates the table when missing, and adds any column the model has gained since
        /// the database was written. Matches how sqlite-net used to auto-migrate.
        /// </summary>
        public async Task EnsureTableAsync<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>()
        {
            await gate.WaitAsync();

            try
            {
                var map = TableMap.For(typeof(T));
                var conn = await Open();

                var columns = map.Columns.Select(p =>
                {
                    string col = Quote(TableMap.ColumnOf(p)) + " " + TableMap.SqlType(p.PropertyType);
                    if (p == map.Key) col += " primary key autoincrement not null";
                    return col;
                });

                await Exec(conn, "create table if not exists " + Quote(map.TableName) + " (" + string.Join(", ", columns) + ")");

                //Existing databases predate some columns - add whatever is missing.
                var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "pragma table_info(" + Quote(map.TableName) + ")";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) present.Add(reader.GetString(1));
                }

                var added = new List<PropertyInfo>();

                foreach (var p in map.Columns)
                {
                    string name = TableMap.ColumnOf(p);
                    if (present.Contains(name)) continue;

                    await Exec(conn, "alter table " + Quote(map.TableName) + " add column " + Quote(name) + " " + TableMap.SqlType(p.PropertyType));
                    added.Add(p);
                }

                //Only backfill columns this call actually added: PropertyInfo order is not
                //guaranteed, and running an update against a column another model later adds
                //would throw "no such column". New tables start empty, so there is nothing
                //to backfill; existing tables only have the rows from before the column.
                foreach (var p in added)
                {
                    var backfill = p.GetCustomAttribute<BackfillAttribute>();
                    if (backfill is null) continue;

                    string literal = backfill.Value switch
                    {
                        bool flag => flag ? "1" : "0",
                        string text => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'",
                        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                        _ => "'" + backfill.Value.ToString().Replace("'", "''", StringComparison.Ordinal) + "'"
                    };

                    await Exec(conn, "update " + Quote(map.TableName) + " set " + Quote(TableMap.ColumnOf(p)) + " = " + literal
                        + " where " + Quote(TableMap.ColumnOf(p)) + " is null");
                }
            }
            finally { gate.Release(); }
        }

        public Task<List<T>> ToListAsync<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>() where T : new()
            => Guarded(() => QueryAsync<T>(null, null));

        public async Task<T> FindAsync<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(int id) where T : new()
        {
            var map = TableMap.For(typeof(T));
            var rows = await Guarded(() => QueryAsync<T>(Quote(map.KeyColumn) + " = $id", new[] { ("$id", (object)id) }));
            return rows.Count > 0 ? rows[0] : default;
        }

        /// <summary>
        /// The rows matching a where clause, which must reference its values through
        /// parameters rather than interpolating them.
        /// </summary>
        public Task<List<T>> WhereAsync<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(string where, params (string, object)[] parameters) where T : new()
            => Guarded(() => QueryAsync<T>(where, parameters));

        public async Task<T> LatestAsync<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>() where T : new()
        {
            var map = TableMap.For(typeof(T));
            var rows = await Guarded(() => QueryAsync<T>(null, null, "order by " + Quote(map.KeyColumn) + " desc limit 1"));
            return rows.Count > 0 ? rows[0] : default;
        }

        private async Task<TResult> Guarded<TResult>(Func<Task<TResult>> operation)
        {
            await gate.WaitAsync();

            try { return await operation(); }
            finally { gate.Release(); }
        }

        private async Task Guarded(Func<Task> operation)
        {
            await gate.WaitAsync();

            try { await operation(); }
            finally { gate.Release(); }
        }

        private async Task<List<T>> QueryAsync<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(string where, (string, object)[] parameters, string tail = null) where T : new()
        {
            var map = TableMap.For(typeof(T));
            var conn = await Open();
            var results = new List<T>();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select * from " + Quote(map.TableName)
                + (where is null ? "" : " where " + where)
                + (tail is null ? "" : " " + tail);

            AddParameters(cmd, parameters);

            using var reader = await cmd.ExecuteReaderAsync();

            //Map ordinals once rather than per row.
            var ordinals = new Dictionary<PropertyInfo, int>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string name = reader.GetName(i);
                var prop = map.Columns.FirstOrDefault(p => string.Equals(TableMap.ColumnOf(p), name, StringComparison.OrdinalIgnoreCase));
                if (prop is not null) ordinals[prop] = i;
            }

            while (await reader.ReadAsync())
            {
                var item = new T();

                foreach (var (prop, ordinal) in ordinals)
                {
                    if (await reader.IsDBNullAsync(ordinal)) continue;
                    prop.SetValue(item, ReadValue(reader, ordinal, prop.PropertyType));
                }

                results.Add(item);
            }

            return results;
        }

        private static object ReadValue(SqliteDataReader reader, int ordinal, Type target)
        {
            target = Nullable.GetUnderlyingType(target) ?? target;

            if (target == typeof(string)) return reader.GetValue(ordinal) as string ?? Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            if (target == typeof(int)) return Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            if (target == typeof(long)) return Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            if (target == typeof(bool)) return Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;
            if (target == typeof(double)) return Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            if (target == typeof(float)) return Convert.ToSingle(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

            if (target == typeof(byte[]))
            {
                object raw = reader.GetValue(ordinal);

                //Oldest databases stored the body as base64 TEXT rather than a BLOB.
                //Hand the raw bytes back and let the decompressor work out the format.
                return raw switch
                {
                    byte[] bytes => bytes,
                    string text => Encoding.UTF8.GetBytes(text),
                    _ => Array.Empty<byte>()
                };
            }

            return reader.GetValue(ordinal);
        }

        public Task<int> InsertAsync<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(T item)
            => Guarded(() => InsertCore(item));

        private async Task<int> InsertCore<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(T item)
        {
            var map = TableMap.For(typeof(T));
            var conn = await Open();

            //Let SQLite assign the key.
            var inserted = map.Columns.Where(p => p != map.Key).ToList();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "insert into " + Quote(map.TableName) + " ("
                + string.Join(", ", inserted.Select(p => Quote(TableMap.ColumnOf(p)))) + ") values ("
                + string.Join(", ", inserted.Select((p, i) => "$p" + i.ToString(CultureInfo.InvariantCulture))) + "); select last_insert_rowid();";

            for (int i = 0; i < inserted.Count; i++)
            {
                cmd.Parameters.AddWithValue("$p" + i.ToString(CultureInfo.InvariantCulture), WriteValue(inserted[i].GetValue(item)));
            }

            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            map.Key?.SetValue(item, id);

            return id;
        }

        public Task UpdateAsync<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(T item)
            => Guarded(() => UpdateCore(item));

        private async Task UpdateCore<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(T item)
        {
            var map = TableMap.For(typeof(T));
            var conn = await Open();

            var updated = map.Columns.Where(p => p != map.Key).ToList();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "update " + Quote(map.TableName) + " set "
                + string.Join(", ", updated.Select((p, i) => Quote(TableMap.ColumnOf(p)) + " = $p" + i.ToString(CultureInfo.InvariantCulture)))
                + " where " + Quote(map.KeyColumn) + " = $key";

            for (int i = 0; i < updated.Count; i++)
            {
                cmd.Parameters.AddWithValue("$p" + i.ToString(CultureInfo.InvariantCulture), WriteValue(updated[i].GetValue(item)));
            }

            cmd.Parameters.AddWithValue("$key", map.Key.GetValue(item));

            await cmd.ExecuteNonQueryAsync();
        }

        public Task DeleteAsync<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(int id)
            => Guarded(() => DeleteCore<T>(id));

        private async Task DeleteCore<[DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(int id)
        {
            var map = TableMap.For(typeof(T));
            var conn = await Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "delete from " + Quote(map.TableName) + " where " + Quote(map.KeyColumn) + " = $id";
            cmd.Parameters.AddWithValue("$id", id);

            await cmd.ExecuteNonQueryAsync();
        }

        private static object WriteValue(object value)
        {
            if (value is null) return DBNull.Value;
            if (value is bool b) return b ? 1 : 0;

            return value;
        }

        /// <summary>
        /// Runs a projection and hands back the raw column values. Lets a caller that needs a
        /// few columns of every session avoid paying for the response bodies stored beside them.
        /// </summary>
        public Task<List<object[]>> RawRowsAsync(string sql, params (string, object)[] parameters)
            => Guarded(() => RawRowsCore(sql, parameters));

        private async Task<List<object[]>> RawRowsCore(string sql, (string, object)[] parameters)
        {
            var conn = await Open();
            var rows = new List<object[]>();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var values = new object[reader.FieldCount];

                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                }

                rows.Add(values);
            }

            return rows;
        }

        //Raw projections come back as object, and SQLite is free to hand an integer column back
        //as a long. These read a value without caring which numeric type it arrived as.
        public static int AsInt(object value) => value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        public static bool AsBool(object value) => value is not null && Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
        public static string AsString(object value) => value as string ?? (value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture));

        public Task ExecuteAsync(string sql, params (string, object)[] parameters)
            => Guarded(() => ExecuteCore(sql, parameters));

        private async Task ExecuteCore(string sql, (string, object)[] parameters)
        {
            var conn = await Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);

            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task Exec(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        private static void AddParameters(SqliteCommand cmd, (string, object)[] parameters)
        {
            if (parameters is null) return;

            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.AddWithValue(name, WriteValue(value));
            }
        }

        public Task<int> ScalarIntAsync(string sql, params (string, object)[] parameters)
            => Guarded(async () =>
            {
                object result = await ScalarCore(sql, parameters);
                return result is null or DBNull ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
            });

        public Task<string> ScalarStringAsync(string sql, params (string, object)[] parameters)
            => Guarded(async () =>
            {
                object result = await ScalarCore(sql, parameters);
                return result is null or DBNull ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
            });

        private async Task<object> ScalarCore(string sql, (string, object)[] parameters)
        {
            var conn = await Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);

            return await cmd.ExecuteScalarAsync();
        }
    }
}
