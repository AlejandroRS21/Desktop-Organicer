using System.Collections.Generic;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.Core.Services;

public class RuleTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Rule> Rules { get; set; } = new();
}

public class RuleTemplateService
{
    public List<RuleTemplate> GetTemplates()
    {
        return new List<RuleTemplate>
        {
            new RuleTemplate
            {
                Name = "Estándar",
                Description = "Organización básica por tipos de archivo comunes (Documentos, Imágenes, Música, Videos).",
                Rules = new List<Rule>
                {
                    new Rule { Name = "Documentos", Priority = 1, IsActive = true, TargetCategory = "Documentos", RuleType = "ExtensionRule", Configuration = "[\".pdf\", \".doc\", \".docx\", \".txt\", \".xlsx\", \".xls\", \".ppt\", \".pptx\"]" },
                    new Rule { Name = "Imágenes", Priority = 2, IsActive = true, TargetCategory = "Imagenes", RuleType = "ExtensionRule", Configuration = "[\".jpg\", \".jpeg\", \".png\", \".gif\", \".bmp\", \".svg\", \".webp\"]" },
                    new Rule { Name = "Videos", Priority = 3, IsActive = true, TargetCategory = "Videos", RuleType = "ExtensionRule", Configuration = "[\".mp4\", \".avi\", \".mkv\", \".mov\", \".wmv\", \".flv\"]" },
                    new Rule { Name = "Música", Priority = 4, IsActive = true, TargetCategory = "Musica", RuleType = "ExtensionRule", Configuration = "[\".mp3\", \".wav\", \".flac\", \".aac\", \".ogg\", \".m4a\"]" },
                    new Rule { Name = "Comprimidos", Priority = 5, IsActive = true, TargetCategory = "Comprimidos", RuleType = "ExtensionRule", Configuration = "[\".zip\", \".rar\", \".7z\", \".tar\", \".gz\"]" }
                }
            },
            new RuleTemplate
            {
                Name = "Desarrollador",
                Description = "Optimizado para código, scripts, binarios y configuraciones.",
                Rules = new List<Rule>
                {
                    new Rule { Name = "Código Fuente", Priority = 1, IsActive = true, TargetCategory = "Codigo", RuleType = "ExtensionRule", Configuration = "[\".cs\", \".py\", \".js\", \".ts\", \".html\", \".css\", \".java\", \".cpp\", \".h\", \".go\", \".rs\"]" },
                    new Rule { Name = "Ejecutables", Priority = 2, IsActive = true, TargetCategory = "Binarios", RuleType = "ExtensionRule", Configuration = "[\".exe\", \".msi\", \".dll\", \".bat\", \".ps1\", \".sh\"]" },
                    new Rule { Name = "Configuración", Priority = 3, IsActive = true, TargetCategory = "Config", RuleType = "ExtensionRule", Configuration = "[\".json\", \".xml\", \".yaml\", \".yml\", \".ini\", \".config\"]" },
                    new Rule { Name = "Data", Priority = 4, IsActive = true, TargetCategory = "Data", RuleType = "ExtensionRule", Configuration = "[\".sql\", \".db\", \".sqlite\", \".csv\"]" },
                    new Rule { Name = "Logs", Priority = 5, IsActive = true, TargetCategory = "Logs", RuleType = "ExtensionRule", Configuration = "[\".log\", \".txt\"]" }
                }
            },
            new RuleTemplate
            {
                Name = "Diseñador",
                Description = "Enfoque en activos gráficos, vectores y proyectos de diseño.",
                Rules = new List<Rule>
                {
                    new Rule { Name = "Vectores", Priority = 1, IsActive = true, TargetCategory = "Vectores", RuleType = "ExtensionRule", Configuration = "[\".svg\", \".ai\", \".eps\"]" },
                    new Rule { Name = "Raster", Priority = 2, IsActive = true, TargetCategory = "Imágenes", RuleType = "ExtensionRule", Configuration = "[\".png\", \".jpg\", \".jpeg\", \".gif\", \".webp\", \".tiff\"]" },
                    new Rule { Name = "Proyectos", Priority = 3, IsActive = true, TargetCategory = "Proyectos", RuleType = "ExtensionRule", Configuration = "[\".psd\", \".indd\", \".fig\", \".sketch\"]" },
                    new Rule { Name = "Fuentes", Priority = 4, IsActive = true, TargetCategory = "Fuentes", RuleType = "ExtensionRule", Configuration = "[\".ttf\", \".otf\", \".woff\", \".woff2\"]" }
                }
            }
        };
    }
}
