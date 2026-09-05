using System.IO;
using System.Text.Json;
using LadderToArduino.Models;

namespace LadderToArduino.Services
{
    public static class ProjectFileService
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static void Save(LadderProgram program, string path)
        {
            string json = JsonSerializer.Serialize(program, Options);
            File.WriteAllText(path, json);
        }

        public static LadderProgram Load(string path)
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LadderProgram>(json, Options) ?? new LadderProgram();
        }

        // In-memory (no file) helpers used for the Undo/Redo stack and rung copy/paste.
        public static string ToJson(LadderProgram program) => JsonSerializer.Serialize(program, Options);

        public static LadderProgram FromJson(string json) =>
            JsonSerializer.Deserialize<LadderProgram>(json, Options) ?? new LadderProgram();

        public static Rung CloneRung(Rung rung)
        {
            string json = JsonSerializer.Serialize(rung, Options);
            return JsonSerializer.Deserialize<Rung>(json, Options);
        }
    }
}
