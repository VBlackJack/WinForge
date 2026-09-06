/*
 * Copyright 2026 Julien Bombled
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinForge.GUI.Helpers;

/// <summary>Preserves unedited profile properties and publishes a complete JSON file atomically.</summary>
internal static class ProfileJsonWriter
{
    public static void Write(string path, IReadOnlyDictionary<string, object> changes, string? sourceJson = null)
    {
        string? existingJson = File.Exists(path) ? File.ReadAllText(path) : sourceJson;
        JsonObject payload = existingJson == null
            ? new JsonObject()
            : JsonNode.Parse(existingJson) as JsonObject
                ?? throw new JsonException("The profile must be a JSON object.");
        foreach (KeyValuePair<string, object> change in changes)
        {
            payload[change.Key] = JsonSerializer.SerializeToNode(change.Value);
        }

        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
