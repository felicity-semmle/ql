using Semmle.Util;
using System;
using System.IO;
using System.Linq;

namespace Semmle.Extraction.Entities
{
    public class File : CachedEntity<string>
    {
        File(Context cx, string path)
            : base(cx, path)
        {
            Path = path;
        }

        public string Path
        {
            get;
            private set;
        }

        public string DatabasePath => PathAsDatabaseId(Path);

        public override bool NeedsPopulation => Context.DefinesFile(Path) || Path == Context.Extractor.OutputPath;

        public override void Populate()
        {
            if (Path == null)
            {
                Context.Emit(Tuples.files(this, "", "", ""));
            }
            else
            {
                var fi = new FileInfo(Path);

                string extension = fi.Extension ?? "";
                string name = fi.Name;
                name = name.Substring(0, name.Length - extension.Length);
                int fromSource = extension.ToLowerInvariant().Equals(".cs") ? 1 : 2;

                // remove the dot from the extension
                if (extension.Length > 0)
                    extension = extension.Substring(1);
                Context.Emit(Tuples.files(this, PathAsDatabaseString(Path), name, extension));

                Context.Emit(Tuples.containerparent(Entities.Folder.Create(Context, fi.Directory), this));
                if (fromSource == 1)
                {
                    foreach (var text in Context.Compilation.SyntaxTrees.
                        Where(t => t.FilePath == Path).
                        Select(tree => tree.GetText()))
                    {
                        var rawText = text.ToString();
                        var lineCounts = LineCounter.ComputeLineCounts(rawText);
                        if (rawText.Length > 0 && rawText[rawText.Length - 1] != '\n') lineCounts.Total++;

                        Context.Emit(Tuples.numlines(this, lineCounts));
                        Context.TrapWriter.Archive(fi.FullName, text.Encoding);
                    }
                }

                Context.Emit(Tuples.file_extraction_mode(this, Context.Extractor.Standalone ? 1 : 0));
            }
        }

        public override IId Id
        {
            get
            {
                return Path == null ?
                    new Key("GENERATED;sourcefile") :
                    new Key(DatabasePath, ";sourcefile");
            }
        }

        /// <summary>
        /// Converts a path string into a string to use as an ID
        /// in the QL database.
        /// </summary>
        /// <param name="path">An absolute path.</param>
        /// <returns>The database ID.</returns>
        public static string PathAsDatabaseId(string path)
        {
            if (path.Length >= 2 && path[1] == ':' && Char.IsLower(path[0]))
                path = Char.ToUpper(path[0]) + "_" + path.Substring(2);
            return path.Replace('\\', '/').Replace(":", "_");
        }

        public static string PathAsDatabaseString(string path) => path.Replace('\\', '/');

        public static File Create(Context cx, string path) => FileFactory.Instance.CreateEntity(cx, path);

        public static File CreateGenerated(Context cx) => GeneratedFile.Create(cx);

        class GeneratedFile : File
        {
            GeneratedFile(Context cx)
                : base(cx, "") { }

            public override bool NeedsPopulation => true;

            public override void Populate()
            {
                Context.Emit(Tuples.files(this, "", "", ""));
            }

            public override IId Id => new Key("GENERATED;sourcefile");

            public static GeneratedFile Create(Context cx) =>
                GeneratedFileFactory.Instance.CreateEntity(cx, null);

            class GeneratedFileFactory : ICachedEntityFactory<string, GeneratedFile>
            {
                public static readonly GeneratedFileFactory Instance = new GeneratedFileFactory();

                public GeneratedFile Create(Context cx, string init) => new GeneratedFile(cx);
            }
        }

        public override Microsoft.CodeAnalysis.Location ReportingLocation => null;

        class FileFactory : ICachedEntityFactory<string, File>
        {
            public static readonly FileFactory Instance = new FileFactory();

            public File Create(Context cx, string init) => new File(cx, init);
        }

        public override TrapStackBehaviour TrapStackBehaviour => TrapStackBehaviour.NoLabel;
    }
}
