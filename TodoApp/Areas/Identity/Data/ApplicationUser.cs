using Microsoft.AspNetCore.Identity;
using TodoApp.Models;

namespace TodoApp.Areas.Identity.Data
{
    public class ApplicationUser : IdentityUser
    {
        public virtual ICollection<TodoItem> TodoItems { get; set; } = new List<TodoItem>();
    }
}