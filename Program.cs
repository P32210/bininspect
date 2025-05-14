using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection;
namespace bininspect
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Missing arguments. Run bininspect --help for more info");
                return;
            }

            switch (args[0])
            {
                case "--version":
                    Console.WriteLine("v0.1");
                    return;
                case "--help":
                    Console.WriteLine(
                        "Syntax:\n" +
                        "   bininspect [--version] [--help] <path> [<flags>]\n" +
                        "   <path>               Path to file or directory.\n" +
                        "   [<flags>]            Flags to apply.\n\n" +
                        "Flags:\n" +
                        "   --help               Display this message.\n" +
                        "   --version            Display the current version of the program.\n" +
                        "   -r, recursive        Check every subcategory of designated path.\n" +
                        "   -b, binaries only    Only check binaries (.exe, .dll) for metadata.\n" +
                        "   -o [<path>], output  Write the output to a file.\n" +
                        "   -v, verbose          Display more detailed information.\n" +
                        "   -e, exceptions       Display any exceptions that the program might catch.\n" +
                        "   -s, style            Add borders and colouring to the output.\n\n" +
                        "Usage:\n" +
                        "   Use the double-dashed flags for different functions.\n" +
                        "   Insert the path to a file to inspect its metadata.\n" +
                        "   Insert the path to a directory to inspect the metadata of all files within.\n" +
                        "   Add flags to modify behaviour.");
                    return;
            }

            if (!File.Exists(args[0]) && !Directory.Exists(args[0]))
            {
                Console.WriteLine($"\"{args[0]}\" is not a file nor directory.");
                return;
            }

            List<string> files = [];
            bool[] flags = new bool[6];
            bool ifFlags = false;
            string outputPath = "";
            ConsoleColor[] colours = new ConsoleColor[8];
            if (args.Length > 1)
                for (int i = 1; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-r":
                            flags[0] = true;
                            ifFlags = true;
                            break;
                        case "-b":
                            flags[1] = true;
                            ifFlags = true;
                            break;
                        case "-o":
                            flags[2] = true;
                            outputPath = args[i + 1];
                            ifFlags = true;
                            break;
                        case "-v":
                            flags[3] = true;
                            ifFlags = true;
                            break;
                        case "-e":
                            flags[4] = true;
                            ifFlags = true;
                            break;
                        case "-s":
                            flags[5] = true;
                            ifFlags = true;
                            break;
                        default:

                            break;
                    }
                }
            if (!ifFlags || flags[5])
                using (StreamReader reader = new($"{AppDomain.CurrentDomain.BaseDirectory}binins.ini"))
                {
                    string nextLine;
                    while ((nextLine = reader.ReadLine()) != null)
                    {
                        if (!ifFlags && nextLine == "[Flags]")
                        {
                            for (byte i = 0; i < flags.Length; i++)
                            {
                                nextLine = reader.ReadLine();
                                if (nextLine.TrimStart()[0] == ';') continue;
                                else nextLine = nextLine.Trim();

                                flags[i] = nextLine.Split('=')[1].Trim() == "TRUE" ? true : false;
                            }
                        }
                        else if (flags[5] && nextLine == "[Styling]")
                        {
                            for (byte i = 0; i < colours.Length; i++)
                            {
                                nextLine = reader.ReadLine();
                                if (nextLine.TrimStart()[0] == ';') continue;
                                else nextLine = nextLine.Trim();

                                colours[i] = (ConsoleColor)byte.Parse(nextLine.Split('=')[1].Trim());
                            }
                        }
                    }
                }
            if (File.Exists(args[0]))
            {
                try
                {
                    if (!flags[1]) files.Add(args[0]);
                    else if (flags[1] && (args[0].EndsWith(".exe") || args[0].EndsWith(".dll")))
                        files.Add(args[0]);
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine($"{args[0]}: Access to file was denied.{(flags[3] ? "" : "\n")}");
                    if (flags[3]) Console.WriteLine($"{e.GetType().FullName}: {e.Message}\n");
                }
                catch (IOException e)
                {
                    Console.WriteLine($"{args[0]}: Failed to access the file.{(flags[3] ? "" : "\n")}");
                    if (flags[3]) Console.WriteLine($"{e.GetType().FullName}: {e.Message}\n");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{args[0]}: Something went wrong.{(flags[3] ? "" : "\n")}");
                    if (flags[3]) Console.WriteLine($"{e.GetType().FullName}: {e.Message}\n");
                }
            }
            else if (Directory.Exists(args[0]))
            {
                if (flags[0])
                    files = Recursive(args[0], flags[1], flags[3]);
                else
                    foreach (string file in Directory.GetFiles(args[0]))
                    {
                        if (flags[1] && !file.EndsWith(".exe") && !file.EndsWith(".dll")) continue;
                        files.Add(file);
                    }
            }

            if (flags[2] && !File.Exists(outputPath))
                Console.Write("Specified output file does not exist. A new file will be created.");
            using StreamWriter? writer = flags[2] ? File.AppendText(outputPath) : null;
            string[] message = new string[3];
            foreach (string file in files)
            {
                try
                {
                    using FileStream sReader = File.OpenRead(file);
                    using PEReader peReader = new(sReader);
                    MetadataReader mReader = peReader.GetMetadataReader();
                    message[0] = (flags[5] ? "O-------------------O\n" : "") +
                    $"Path: {file}" + 
                    (peReader.HasMetadata ? "\nFile has metadata" : "File doesn't have metadata") +
                    (mReader.IsAssembly ? "\nFile is a .NET assembly" : "File isn't a .NET assembly");
                    if (peReader.HasMetadata)
                    {
                        foreach (TypeDefinitionHandle handle in mReader.TypeDefinitions)
                        {
                            TypeDefinition type = mReader.GetTypeDefinition(handle);
                            message[0] += $"\nNamespace {mReader.GetString(type.Namespace)}" +
                                "\n    ";
                            if (flags[2])
                                foreach (TypeAttributes val in Enum.GetValues(typeof(TypeAttributes)))
                                    if (val != 0 && type.Attributes.HasFlag(val))
                                        message[0] += $"{val} ";
                            message[0] += $"class {mReader.GetString(type.Name)}";
                            foreach (FieldDefinitionHandle fHandle in type.GetFields())
                            {
                                FieldDefinition field = mReader.GetFieldDefinition(fHandle);
                                message[0] += "\n        ";
                                if (flags[2])
                                    foreach (FieldAttributes val in Enum.GetValues(typeof(FieldAttributes)))
                                        if (val != 0 && field.Attributes.HasFlag(val))
                                            message[0] += $"{val} ";
                                message[0] += $"{mReader.GetString(mReader.GetTypeDefinition(field.GetDeclaringType()).Name)} {mReader.GetString(field.Name)}";
                            }
                            foreach (MethodDefinitionHandle mHandle in type.GetMethods())
                            {
                                MethodDefinition method = mReader.GetMethodDefinition(mHandle);
                                message[0] += "\n        ";
                                if (flags[2])
                                    foreach (MethodAttributes val in Enum.GetValues(typeof(MethodAttributes)))
                                        if (val != 0 && method.Attributes.HasFlag(val))
                                            message[0] += $"{val} ";
                                message[0] += $"{mReader.GetString(mReader.GetTypeDefinition(method.GetDeclaringType()).Name)} {mReader.GetString(method.Name)}(";
                                foreach (ParameterHandle pHandle in method.GetParameters())
                                {
                                    Parameter param = mReader.GetParameter(pHandle);
                                    message[0] += $"{mReader.GetString(param.Name)}, ";
                                }
                                message[0] += ")";
                            }
                        }
                    }
                    message[1] = "";
                    if (flags[2])
                        foreach (LocalConstantHandle handle in mReader.LocalConstants)
                            message[1] += $"\nconst {mReader.GetString(mReader.GetLocalConstant(handle).Name)}";
                    message[2] = (peReader.IsEntireImageAvailable ? "\nEntire PE image is available" : "\nCannot access full PE image") +
                        (flags[5] ? "\nO-------------------O\n" : "\n");
                }
                catch (IOException e)
                {
                    if (flags[4])
                    {
                        Console.WriteLine($"{file}: Failed to access the file.{(flags[3] ? "" : "\n")}");
                        if (flags[3]) Console.WriteLine($"{e.GetType().FullName}: {e.Message}\n");
                    }
                    continue;
                }
                catch (Exception e)
                {
                    if (flags[4])
                    {
                        Console.WriteLine($"{file}: Something went wrong.{(flags[3] ? "" : "\n")}");
                        if (flags[3]) Console.WriteLine($"{e.GetType().FullName}: {e.Message}\n");
                    }
                    continue;
                }
                if (flags[2])
                {
                    writer.WriteLine(message[0]);
                    writer.WriteLine(message[1]);
                    writer.WriteLine(message[2]);
                    continue;
                }

                Console.WriteLine(message[0]);
                if (flags[3])
                    Console.WriteLine(message[1]);
                Console.WriteLine(message[2]);
            }
            writer?.Close();
        }
        private static List<string> Recursive(string path, bool binaries, bool verbose)
        {
            List<string> files = [];
            try
            {
                string[] paths = Directory.GetFiles(path);
                foreach (string file in paths)
                {
                    if (binaries && !file.EndsWith(".exe") && !file.EndsWith(".dll")) continue;
                    files.Add(file);
                }
                if (Directory.GetDirectories(path).Length > 0)
                {
                    foreach (string directory in Directory.GetDirectories(path))
                    {
                        paths = Recursive(directory, binaries, verbose).ToArray();
                        foreach (string file in paths)
                        {
                            if (binaries && !file.EndsWith(".exe") && !file.EndsWith(".dll")) continue;
                            files.Add(file);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine($"{e.Source}: Access to file was denied.{(verbose ? "" : "\n")}");
                if (verbose) Console.WriteLine($"{e.GetType().FullName}: {e.Message}\n");
            }
            catch (IOException e)
            {
                Console.WriteLine($"{e.Source}: Failed to access the file.{(verbose ? "" : "\n")}");
                if (verbose) Console.WriteLine($"{e.GetType().FullName}: {e.Message}\n");
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Source}: Something went wrong.{(verbose ? "" : "\n")}");
                if (verbose) Console.WriteLine($"{e.GetType().FullName}: {e.Message}\n");
            }
            return files;
        }
    }
}