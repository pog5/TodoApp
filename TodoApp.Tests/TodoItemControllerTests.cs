using System.Linq.Expressions;
using System.Security.Claims;
using EntityFrameworkCore.Testing.Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using TodoApp.Areas.Identity.Data;
using TodoApp.Controllers;
using TodoApp.Data;
using TodoApp.Models;

namespace TodoApp.Tests;

public class TodoItemControllerTests
{
    private readonly TodoItemController _controller;
    private readonly ApplicationDbContext _mockedDbContext;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly ApplicationUser _testUser;
    private readonly string _testUserId = "test-user-id";

    public TodoItemControllerTests()
    {
        // Mock UserManager
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager =
            new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        _testUser = new ApplicationUser { Id = _testUserId, UserName = "test@example.com" };
        _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(_testUser);
        _mockUserManager.Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(_testUserId);

        // Mock ApplicationDbContext
        _mockedDbContext = Create.MockedDbContextFor<ApplicationDbContext>();

        // Setup default user for controller context
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId),
            new Claim(ClaimTypes.Name, "test@example.com")
        }, "mock"));

        _controller = new TodoItemController(_mockedDbContext, _mockUserManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    // Helper method to create a mock DbSet
    private static Mock<DbSet<T>> CreateMockDbSet<T>(IQueryable<T> entities) where T : class
    {
        var mockSet = new Mock<DbSet<T>>();
        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<T>(entities.Provider));
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(entities.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(entities.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => entities.GetEnumerator());

        // For async operations like ToListAsync, FirstOrDefaultAsync
        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(entities.GetEnumerator()));

        // Setup Add, Remove, etc. if you need to verify them on the DbSet directly,
        // though often you verify them on the DbContext mock.
        mockSet.Setup(m => m.Add(It.IsAny<T>())).Verifiable();
        mockSet.Setup(m => m.Remove(It.IsAny<T>())).Verifiable();

        return mockSet;
    }

    [Fact]
    public async Task Index_ReturnsViewResult_WithListOfTodoItems_ForCurrentUser()
    {
        // Arrange
        var expectedUserItems = new List<TodoItem>
        {
            // It's good practice to initialize all relevant properties, even if not directly asserted,
            // to make the test data clear and complete.
            new()
            {
                Id = 1, Title = "User1 Task 1", UserId = _testUserId, CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsDone = false, User = _testUser
            },
            new()
            {
                Id = 3, Title = "User1 Task 2", UserId = _testUserId, CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                IsDone = true, User = _testUser
            }
        };
        var otherUserItem = new TodoItem
        {
            Id = 2, Title = "Another User Task", UserId = "other-user-id", CreatedAt = DateTime.UtcNow, IsDone = false
        };

        _mockedDbContext.Set<TodoItem>().AddRange(expectedUserItems);
        _mockedDbContext.Set<TodoItem>().Add(otherUserItem);
        await _mockedDbContext.SaveChangesAsync();

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<TodoItem>>(viewResult.Model); // Use viewResult.Model directly

        // Convert to a list for easier assertions if needed, but Count() and All() work on IEnumerable
        var actualUserItems = model.ToList();

        Assert.Equal(expectedUserItems.Count, actualUserItems.Count);
        foreach (var expectedItem in expectedUserItems)
            // Assert that each expected item is present in the actual results.
            // This is more robust than just checking UserId if titles or other properties matter.
            Assert.Contains(actualUserItems, actualItem =>
                actualItem.Id == expectedItem.Id &&
                actualItem.Title == expectedItem.Title &&
                actualItem.UserId == _testUserId);

        // Additionally, ensure no items from other users are present
        Assert.DoesNotContain(actualUserItems, actualItem => actualItem.Id == otherUserItem.Id);
    }

    [Fact]
    public async Task Create_Post_ValidModel_AddsTodoItemAndRedirects()
    {
        // Arrange
        var todoItemToCreate = new TodoItem
        {
            Title = "New Test Todo from Create",
            DueDate = DateTime.UtcNow.AddDays(1),
            IsDone = false // Explicitly set default for clarity, though controller should also set it.
        };

        // Act
        var result = await _controller.Create(todoItemToCreate);

        // Assert
        // 1. Check for correct redirection
        var redirectToActionResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(TodoItemController.Index), redirectToActionResult.ActionName); // Use nameof for type safety

        // 2. Verify the item was added to the (mocked) database with correct properties

        var addedItem = _mockedDbContext.Set<TodoItem>()
            .FirstOrDefault(t => t.Title == todoItemToCreate.Title && t.UserId == _testUserId);

        Assert.NotNull(addedItem);
        // Assert properties set by the controller or defaults
        Assert.Equal(_testUserId, addedItem.UserId);
        Assert.False(addedItem.IsDone); // Controller sets this to false
        Assert.Equal(todoItemToCreate.Title, addedItem.Title);
        Assert.Equal(todoItemToCreate.DueDate, addedItem.DueDate);
        Assert.True(
            addedItem.CreatedAt >= DateTime.UtcNow.AddMinutes(-1) &&
            addedItem.CreatedAt <= DateTime.UtcNow.AddMinutes(1),
            "CreatedAt should be set to a recent UTC time.");
    }

    [Fact]
    public async Task Create_Post_InvalidModel_ReturnsViewWithModel()
    {
        // Arrange
        var newTodoItemVM = new TodoItem();
        _controller.ModelState.AddModelError("Title", "Title is required");

        // Act
        var result = await _controller.Create(newTodoItemVM);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(newTodoItemVM, viewResult.Model); 
    }

    [Fact]
    public async Task Edit_Get_ItemFound_ReturnsViewWithItem()
    {
        // Arrange
        var itemId = 1;
        var existingItem = new TodoItem
            { Id = itemId, Title = "Todo Item", UserId = _testUserId, CreatedAt = DateTime.UtcNow };

        _mockedDbContext.Set<TodoItem>().AddRange(existingItem);
        await _mockedDbContext.SaveChangesAsync();

        // Act
        var result = await _controller.Edit(itemId);
        await _mockedDbContext.SaveChangesAsync();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        // If Edit GET maps to a ViewModel:
        var model = Assert.IsType<TodoItem>(viewResult.Model);
        Assert.Equal(itemId, model.Id);
        Assert.Equal(existingItem.Title, model.Title);
        // If Edit GET returns the TodoItem directly:
        // var model = Assert.IsType<TodoItem>(viewResult.Model);
        // Assert.Equal(itemId, model.Id);
    }

    [Fact]
    public async Task Edit_Get_ItemNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var itemId = 99; // Non-existent item

        // Act
        var result = await _controller.Edit(itemId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_ItemNotBelongingToUser_ReturnsNotFoundResult()
    {
        // Arrange
        var itemId = 1;
        var existingItem = new TodoItem
            { Id = itemId, Title = "Other User Item", UserId = "other-user-id", CreatedAt = DateTime.UtcNow };

        // Seed data into the mocked DbContext's DbSet
        _mockedDbContext.Set<TodoItem>().AddRange(existingItem); // <--- Use Set<T>()
        // No need to call SaveChanges for query tests with this library typically

        // Act
        var result = await _controller.Edit(itemId);

        // Assert
        Assert.IsType<NotFoundResult>(result); // Or NotFoundResult based on your controller
    }


    // TODO: Add tests for Edit (POST) valid and invalid, Delete (GET and POST), ToggleDone, etc.
    // For Edit (POST) valid:
    // 1. Arrange: Setup existing item, mock FindAsync/FirstOrDefaultAsync to return it.
    //    Setup SaveChangesAsync.
    // 2. Act: Call Edit POST with valid view model.
    // 3. Assert: RedirectToActionResult, SaveChangesAsync called, properties on the (mocked) item updated.
}

// Helper class for IAsyncEnumerable needed for EF Core 5+ mocking of async LINQ operations
internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        return new TestAsyncEnumerable<TEntity>(expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new TestAsyncEnumerable<TElement>(expression);
    }

    public object Execute(Expression expression)
    {
        return _inner.Execute(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return _inner.Execute<TResult>(expression);
    }

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        return Execute<TResult>(expression);
    }

    public Task<object> ExecuteAsync(Expression expression, CancellationToken cancellationToken)
    {
        return Task.FromResult(Execute(expression));
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable)
    {
    }

    public TestAsyncEnumerable(Expression expression) : base(expression)
    {
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner)
    {
        _inner = inner;
    }

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(_inner.MoveNext());
    }
}