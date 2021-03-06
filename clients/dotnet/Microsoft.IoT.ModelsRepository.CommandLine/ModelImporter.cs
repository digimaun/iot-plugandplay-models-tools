﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.IoT.ModelsRepository;
using System;
using System.IO;

namespace Microsoft.IoT.ModelsRepository.CommandLine
{
    internal class ModelImporter
    {
        public static void Import(string modelContent, DirectoryInfo repository)
        {
            string rootId = ParsingUtils.GetRootId(modelContent);
            string createPath = DtmiConventions.GetModelUri(rootId, new Uri(repository.FullName)).AbsolutePath;

            Outputs.WriteOut($"- Importing model \"{rootId}\"...");
            if (File.Exists(createPath))
            {
                Outputs.WriteOut(
                    $"- Skipping \"{rootId}\". Model file already exists in repository.",
                    ConsoleColor.DarkCyan);
                return;
            }

            (new FileInfo(createPath)).Directory.Create();
            Outputs.WriteToFile(createPath, modelContent);
        }
    }
}
