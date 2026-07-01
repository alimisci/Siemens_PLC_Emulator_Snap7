using System;
using System.Collections.Generic;
using System.Linq;
using Snap7;

namespace S7Emulator
{
    /// <summary>
    /// Manages multiple DBs, defining them and sharing them live via S7Server.
    /// NOTE: The RegisterArea/UnregisterArea signatures may differ slightly depending on
    /// the version of Snap7.net.cs you use (e.g. it might expect "byte[]" instead of "ref byte[]").
    /// If you get a build error, just adjust these two methods to match the signature in
    /// your own Snap7.net.cs file.
    /// </summary>
    public class DbManager
    {
        public S7Server Server { get; } = new S7Server();
        public Dictionary<int, DbDefinition> Dbs { get; } = new Dictionary<int, DbDefinition>();

        public bool IsRunning { get; private set; }

        public DbDefinition AddDb(int number, string name)
        {
            if (Dbs.ContainsKey(number))
                throw new InvalidOperationException($"DB{number} is already defined.");

            var db = new DbDefinition { Number = number, Name = name };
            db.RecalculateLayout(); // creates a minimal 1-byte empty buffer
            Dbs[number] = db;

            if (IsRunning)
                RegisterDb(db);

            return db;
        }

        public void RemoveDb(int number)
        {
            if (!Dbs.TryGetValue(number, out var db)) return;

            if (IsRunning)
                UnregisterDb(db);

            Dbs.Remove(number);
        }

        /// <summary>
        /// Clears all DBs (used before loading a project file).
        /// Unregisters them first if the server is currently running.
        /// </summary>
        public void ClearAll()
        {
            if (IsRunning)
            {
                foreach (var db in Dbs.Values.ToList())
                    UnregisterDb(db);
            }
            Dbs.Clear();
        }

        /// <summary>
        /// Call this whenever a DB's field list changes (add/remove/type change).
        /// Rebuilds the buffer and refreshes the server registration.
        /// </summary>
        public void RebuildDb(int number)
        {
            if (!Dbs.TryGetValue(number, out var db)) return;

            bool wasRegistered = IsRunning;
            if (wasRegistered)
                UnregisterDb(db);

            db.RecalculateLayout();

            if (wasRegistered)
                RegisterDb(db);
        }

        public int Start()
        {
            foreach (var db in Dbs.Values)
                RegisterDb(db);

            int result = Server.Start();
            if (result == 0) IsRunning = true;
            return result;
        }

        public void Stop()
        {
            Server.Stop();
            IsRunning = false;
        }

        public string ErrorText(int errorCode) => Server.ErrorText(errorCode);

        private void RegisterDb(DbDefinition db)
        {
            var buffer = db.Buffer;
            Server.RegisterArea(S7Server.srvAreaDB, db.Number, ref buffer, db.Buffer.Length);
        }

        private void UnregisterDb(DbDefinition db)
        {
            Server.UnregisterArea(S7Server.srvAreaDB, db.Number);
        }

        public string ReadFieldAsString(int dbNumber, DbFieldDefinition field)
        {
            if (!Dbs.TryGetValue(dbNumber, out var db)) return "";
            return S7ByteConverter.ReadFieldAsString(db.Buffer, field);
        }

        public bool TryWriteField(int dbNumber, DbFieldDefinition field, string text)
        {
            if (!Dbs.TryGetValue(dbNumber, out var db)) return false;
            return S7ByteConverter.TryWriteFieldFromString(db.Buffer, field, text);
        }
    }
}
