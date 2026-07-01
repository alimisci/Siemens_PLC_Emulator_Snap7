using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace S7Emulator
{
    public class DbFieldDto
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "Bool";
        public int StringLength { get; set; } = 20;
    }

    public class DbDefinitionDto
    {
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public List<DbFieldDto> Fields { get; set; } = new();

        /// <summary>
        /// Base64 snapshot of the byte buffer (optional). If present, reopening the file
        /// restores the last live values along with the field definitions.
        /// </summary>
        public string? BufferBase64 { get; set; }
    }

    public class ProjectFileDto
    {
        public string FormatVersion { get; set; } = "1.0";
        public List<DbDefinitionDto> Databases { get; set; } = new();
    }

    /// <summary>
    /// Saves the DB definitions inside a DbManager to a JSON file (and reloads them),
    /// optionally including current live values.
    /// </summary>
    public static class ProjectSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true
        };

        public static void Save(string path, DbManager manager, bool includeValues)
        {
            var dto = new ProjectFileDto();

            foreach (var db in manager.Dbs.Values.OrderBy(d => d.Number))
            {
                var dbDto = new DbDefinitionDto { Number = db.Number, Name = db.Name };

                foreach (var field in db.Fields)
                {
                    dbDto.Fields.Add(new DbFieldDto
                    {
                        Name = field.Name,
                        DataType = field.DataType.ToString(),
                        StringLength = field.StringLength
                    });
                }

                if (includeValues)
                    dbDto.BufferBase64 = Convert.ToBase64String(db.Buffer);

                dto.Databases.Add(dbDto);
            }

            string json = JsonSerializer.Serialize(dto, Options);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Loads the DB definitions from a file into the manager, replacing any existing DBs.
        /// </summary>
        public static void Load(string path, DbManager manager)
        {
            string json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<ProjectFileDto>(json, Options)
                      ?? throw new InvalidDataException("Could not read the file, or the format is invalid.");

            manager.ClearAll();

            foreach (var dbDto in dto.Databases.OrderBy(d => d.Number))
            {
                var db = manager.AddDb(dbDto.Number, dbDto.Name);

                foreach (var fieldDto in dbDto.Fields)
                {
                    if (!Enum.TryParse<S7DataType>(fieldDto.DataType, out var type))
                        type = S7DataType.Bool; // safe fallback for unrecognized types

                    db.Fields.Add(new DbFieldDefinition
                    {
                        Name = fieldDto.Name,
                        DataType = type,
                        StringLength = fieldDto.StringLength
                    });
                }

                manager.RebuildDb(db.Number); // recompute offsets and buffer size based on the loaded fields

                if (!string.IsNullOrEmpty(dbDto.BufferBase64))
                {
                    try
                    {
                        byte[] savedBuffer = Convert.FromBase64String(dbDto.BufferBase64);
                        int copyLen = Math.Min(savedBuffer.Length, db.Buffer.Length);
                        Array.Copy(savedBuffer, db.Buffer, copyLen);
                    }
                    catch
                    {
                        // Buffer size/format mismatch: skip values but keep the definitions
                    }
                }
            }
        }
    }
}
