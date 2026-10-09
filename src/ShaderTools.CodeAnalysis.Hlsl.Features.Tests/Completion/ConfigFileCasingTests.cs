using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using ShaderTools.CodeAnalysis.Text;
using ShaderTools.Testing.Workspaces;
using Xunit;

namespace ShaderTools.CodeAnalysis.Hlsl.Tests.Options
{
    public class ConfigFileCasingTests : IDisposable
    {
        private readonly string _root;

        public ConfigFileCasingTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "HlslToolsCasing-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        [Fact]
        public void IncludeDirectoriesKeepTheirCasing()
        {
            // The config cache is keyed on the lower-cased directory. That key must not be used as
            // the path the config is actually loaded from: every include resolved relative to it
            // inherits the casing, and the editor is then handed lower-cased URIs that don't match
            // the documents it already has open.
            var shaderDir = Path.Combine(_root, "Assets", "Shaders", "PostProcess");
            Directory.CreateDirectory(shaderDir);

            File.WriteAllText(
                Path.Combine(shaderDir, "shadertoolsconfig.json"),
                @"{ ""root"": true, ""hlsl.additionalIncludeDirectories"": [ ""ShaderLib"", "".."" ] }");

            var workspace = new TestWorkspace();
            var sourceFile = new SourceFile(SourceText.From(""), Path.Combine(shaderDir, "Test.hlsl"));

            var configFile = workspace.LoadConfigFile(sourceFile);

            Assert.Equal(
                new[]
                {
                    Path.Combine(shaderDir, "ShaderLib"),
                    Path.Combine(_root, "Assets", "Shaders"),
                },
                configFile.HlslAdditionalIncludeDirectories.ToArray());
        }
    }
}
