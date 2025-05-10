using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TodoApp.Areas.Identity.Data;
using TodoApp.Data;
using TodoApp.Models;

namespace TodoApp.Controllers
{
    [Authorize]
    public class TodoItemController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TodoItemController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager; 
        }

        // GET: TodoItem
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.TodoItems
                .Include(t => t.User)
                .OrderByDescending(t => t.CreatedAt);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: TodoItem/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var todoItem = await _context.TodoItems
                .Include(t => t.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (todoItem == null)
            {
                return NotFound();
            }

            return View(todoItem);
        }

        // GET: TodoItem/Create
        public IActionResult Create()
        {
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: TodoItem/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,DueDate")] TodoItem todoItem)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Challenge(); // this should not happen, but just in case
            }
            
            todoItem.UserId = userId;
            todoItem.CreatedAt = DateTime.UtcNow;
            todoItem.IsDone = false;
            
            ModelState.Remove("UserId");
            ModelState.Remove("User");
            
            if (ModelState.IsValid)
            {
                _context.Add(todoItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", todoItem.UserId);
            return View(todoItem);
        }

        // GET: TodoItem/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            var todoItem = await _context.TodoItems
                .Where(m => m.Id == id)
                .FirstOrDefaultAsync(m => m.Id == id &&  m.UserId == userId);
            if (todoItem == null)
            {
                return NotFound();
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", todoItem.UserId);
            return View(todoItem);
        }

        // POST: TodoItem/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,CreatedAt,Title,IsDone,DueDate")] TodoItem todoItem)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (id != todoItem.Id)
            {
                ModelState.AddModelError(string.Empty, "Item ID does not match object's id");
                return NotFound($"Item ID {id} does not match object's id {todoItem.Id}");
            }

            var itemToUpdate = await _context.TodoItems.FirstOrDefaultAsync(i => i.Id == id && i.UserId == currentUserId);
            if (itemToUpdate == null)
            {
                ModelState.AddModelError(string.Empty, "Item does not exist in DB.");
                return NotFound("Item does not exist in DB");
            }
            
            if (ModelState.IsValid)
            {
                itemToUpdate.Title = todoItem.Title;
                itemToUpdate.IsDone = todoItem.IsDone;
                itemToUpdate.DueDate = todoItem.DueDate;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TodoItemExists(todoItem.Id))
                    {
                        ModelState.AddModelError(string.Empty, "The item has been deleted.");
                        return NotFound("The item has been deleted");
                    }
                    ModelState.AddModelError(string.Empty, "The item was modified. Please review the changes and try again.");
                    _context.Entry(itemToUpdate).Reload(); 
                    return View(itemToUpdate); 
                }
                return RedirectToAction(nameof(Index));
            }

            itemToUpdate.Title = todoItem.Title;
            itemToUpdate.IsDone = todoItem.IsDone;
            itemToUpdate.DueDate = todoItem.DueDate;
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", todoItem.UserId);
            return View(todoItem);
        }

        // GET: TodoItem/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            var todoItem = await _context.TodoItems
                .Include(t => t.User)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            if (todoItem == null)
            {
                return NotFound();
            }

            return View(todoItem);
        }

        // POST: TodoItem/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var todoItem = await _context.TodoItems.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            if (todoItem == null)
            {
                return NotFound();
            }

            _context.TodoItems.Remove(todoItem);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        
        // helper to toggle IsDone directly from the Index page
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDone(int id)
        {
            var userId = _userManager.GetUserId(User);
            var todoItem = await _context.TodoItems.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

            if (todoItem == null)
            {
                return NotFound();
            }

            todoItem.IsDone = !todoItem.IsDone;
            _context.Update(todoItem);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool TodoItemExists(int id)
        {
            var userId = _userManager.GetUserId(User);
            return _context.TodoItems.Any(e => e.Id == id  && e.UserId == userId);
        }
    }
}
