using System.Collections.Generic;

namespace DesktopOrganizer.Core.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    
    // Navigation property for subcategories if needed, though instructions mention folder nesting
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
}
