﻿using Azure.IoT.DeviceModelsRepository.Resolver;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Text.Json;

namespace Azure.IoT.DeviceModelsRepository.CLI
{
    public class CommonOptions
    {
        readonly static string _defaultRepository = ResolverClient.DefaultRepository;

        public static Option<string> Dtmi
        {
            get
            {
                Option<string> dtmiOption = new Option<string>(
                    "--dtmi",
                    description: "Digital Twin Model Identifier. Example: 'dtmi:com:example:Thermostat;1' ");

                dtmiOption.AddValidator(option =>
                {
                    string value = option.GetValueOrDefault<string>();
                    if (!ResolverClient.IsValidDtmi(value))
                    {
                        return $"Invalid dtmi format '{value}'.";
                    }
                    return null;
                });

                dtmiOption.Argument = new Argument<string>
                {
                    Arity = ArgumentArity.ExactlyOne
                };

                return dtmiOption;
            }
        }

        public static Option<string> Repo
        {
            get
            {
                Option<string> repoOption = new Option<string>(
                    "--repo",
                    description: "Model Repository location. Supports remote endpoint or local directory. ",
                    getDefaultValue: () => ResolverClient.DefaultRepository);

                return repoOption;
            }
        }

        public static Option<DirectoryInfo> LocalRepo
        {
            get
            {
                return new Option<DirectoryInfo>(
                  "--local-repo",
                  description: "Local Model Repository path. If no path is provided the current working directory is used. ",
                  getDefaultValue: () => null)
                {
                    Argument = new Argument<DirectoryInfo>().ExistingOnly()
                };
            }
        }

        public static Option<string> Output
        {
            get
            {
                return new Option<string>(
                    new string[] { "--output", "-o" },
                    description: "Desired file path to write result contents. ",
                    getDefaultValue: () => null
                    );
            }
        }

        public static Option<FileInfo> ModelFile
        {
            get
            {
                return new Option<FileInfo>(
                    "--model-file",
                    description: "Path to file containing Digital Twins model content. ")
                {
                    Argument = new Argument<FileInfo>().ExistingOnly()
                };
            }
        }

        public static Option<bool> Silent
        {
            get
            {
                return new Option<bool>(
                    "--silent",
                    description: "Silences command result output on stdout.",
                    getDefaultValue: () => false)
                {
                    Argument = new Argument<bool>
                    {
                        Arity = ArgumentArity.ZeroOrOne
                    },
                };
            }
        }

        public static Option<bool> Strict
        {
            get
            {
                return new Option<bool>(
                    "--strict",
                    description: "Runs additional verifications for a model including file paths, DTMI scoping and reserved words.",
                    getDefaultValue: () => false)
                {
                    Argument = new Argument<bool>
                    {
                        Arity = ArgumentArity.ZeroOrOne
                    },
                };
            }
        }

        public static JsonDocumentOptions DefaultJsonParseOptions
        {
            get
            {
                return new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                };
            }
        }

        public static JsonSerializerOptions DefaultJsonSerializerOptions
        {
            get
            {
                return new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    WriteIndented = true,
                };
            }
        }
    }
}
