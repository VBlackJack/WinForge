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

using System.Reflection;
using WinForge.GUI.Models;
using WinForge.GUI.Services;

namespace WinForge.GUI.Tests;

public sealed class HybridDetectionServiceTests
{
    [Fact]
    public void ParseWingetUpgradeOutput_ShouldHandleFrenchCliTable()
    {
        const string output = """
Nom                                     ID                                 Version            Disponible         Source
-----------------------------------------------------------------------------------------------------------------------
7-Zip 25.01 (x64)                       7zip.7zip                          25.01              26.01              winget
TreeSize Free V4.7.3 (64 bit)           JAMSoftware.TreeSize.Free          < 4.8.1.610        4.8.1.610          winget
101 mises a niveau disponibles.

Les packages suivants ont une mise a niveau disponible, mais necessitent un ciblage explicite pour la mise a niveau:
Nom                       ID                    Version Disponible Source
-------------------------------------------------------------------------
Chocolatey (Install Only) Chocolatey.Chocolatey 2.6.0.0 2.7.3.0    winget
""";
        MethodInfo method = typeof(HybridDetectionService).GetMethod(
            "ParseWingetUpgradeOutput",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        List<UpdateInfo> updates = Assert.IsAssignableFrom<List<UpdateInfo>>(method.Invoke(null, [output]));

        Assert.Equal(3, updates.Count);
        Assert.Contains(
            updates,
            update =>
                update.Id == "7zip.7zip" &&
                update.Name == "7-Zip 25.01 (x64)" &&
                update.CurrentVersion == "25.01" &&
                update.NewVersion == "26.01" &&
                update.Source == "winget");
        Assert.Contains(
            updates,
            update =>
                update.Id == "JAMSoftware.TreeSize.Free" &&
                update.CurrentVersion == "< 4.8.1.610" &&
                update.NewVersion == "4.8.1.610");
        Assert.Contains(updates, update => update.Id == "Chocolatey.Chocolatey");
    }
}
