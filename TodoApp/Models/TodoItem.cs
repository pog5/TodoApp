using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TodoApp.Areas.Identity.Data;

namespace TodoApp.Models
{
    [DisplayName("TODO")]
    public class TodoItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [DisplayName("Completed?")]
        public bool IsDone { get; set; } = false;

        [DisplayName("Added at:")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [DisplayName("Due by:")]
        public DateTime? DueDate { get; set; }

        [Required]
        [DisplayName("Creator's ID:")]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("Creator")]
        public virtual ApplicationUser? User { get; set; }
    }
}