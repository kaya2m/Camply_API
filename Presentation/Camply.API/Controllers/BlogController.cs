using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Camply.Application.Blogs.DTOs;
using Camply.Application.Blogs.Interfaces;
using Camply.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Camply.API.Controllers
{
    [ApiController]
    [Route("api/blogs")]
    public class BlogController : ControllerBase
    {
        private readonly IBlogService _blogService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<BlogController> _logger;

        public BlogController(
            IBlogService blogService,
            ICurrentUserService currentUserService,
            ILogger<BlogController> logger)
        {
            _blogService = blogService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Gets a paged list of published blogs
        /// </summary>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <param name="sortBy">Sort order (default: recent, options: recent, popular, comments, likes)</param>
        /// <returns>Paged list of blogs</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<BlogSummaryResponse>>> GetBlogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortBy = "recent")
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 50) pageSize = 50;

                var blogs = await _blogService.GetBlogsAsync(page, pageSize, sortBy, _currentUserService.UserId);
                return Ok(blogs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blogs");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving blogs" });
            }
        }

        /// <summary>
        /// Gets a paged list of blogs by a specific user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of blogs by user</returns>
        [HttpGet("by-user/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedResponse<BlogSummaryResponse>>> GetBlogsByUser(
            Guid userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 50) pageSize = 50;

                var blogs = await _blogService.GetBlogsByUserAsync(userId, page, pageSize, _currentUserService.UserId);
                return Ok(blogs);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blogs for user ID {userId}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving blogs" });
            }
        }

        /// <summary>
        /// Gets a paged list of blogs with a specific tag
        /// </summary>
        /// <param name="tag">Tag name or slug</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of blogs with tag</returns>
        [HttpGet("by-tag/{tag}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<BlogSummaryResponse>>> GetBlogsByTag(
            string tag,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // Validate parameters
                if (string.IsNullOrWhiteSpace(tag))
                {
                    return BadRequest(new { message = "Tag cannot be empty" });
                }

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 50) pageSize = 50;

                var blogs = await _blogService.GetBlogsByTagAsync(tag, page, pageSize, _currentUserService.UserId);
                return Ok(blogs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blogs for tag '{tag}'");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving blogs" });
            }
        }

        /// <summary>
        /// Gets a paged list of blogs in a specific category
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of blogs in category</returns>
        [HttpGet("by-category/{categoryId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedResponse<BlogSummaryResponse>>> GetBlogsByCategory(
            Guid categoryId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 50) pageSize = 50;

                var blogs = await _blogService.GetBlogsByCategoryAsync(categoryId, page, pageSize, _currentUserService.UserId);
                return Ok(blogs);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blogs for category ID {categoryId}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving blogs" });
            }
        }

        /// <summary>
        /// Gets a paged list of blogs from a specific location
        /// </summary>
        /// <param name="locationId">Location ID</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of blogs from location</returns>
        [HttpGet("by-location/{locationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedResponse<BlogSummaryResponse>>> GetBlogsByLocation(
            Guid locationId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 50) pageSize = 50;

                var blogs = await _blogService.GetBlogsByLocationAsync(locationId, page, pageSize, _currentUserService.UserId);
                return Ok(blogs);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blogs for location ID {locationId}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving blogs" });
            }
        }

        /// <summary>
        /// Gets a specific blog by ID
        /// </summary>
        /// <param name="id">Blog ID</param>
        /// <returns>Blog details</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BlogDetailResponse>> GetBlogById(Guid id)
        {
            try
            {
                var blog = await _blogService.GetBlogByIdAsync(id, _currentUserService.UserId);

                // Increment view count asynchronously (fire and forget)
                _ = _blogService.IncrementViewCountAsync(id);

                return Ok(blog);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blog with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the blog" });
            }
        }

        /// <summary>
        /// Gets a specific blog by slug
        /// </summary>
        /// <param name="slug">Blog slug</param>
        /// <returns>Blog details</returns>
        [HttpGet("by-slug/{slug}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BlogDetailResponse>> GetBlogBySlug(string slug)
        {
            try
            {
                var blog = await _blogService.GetBlogBySlugAsync(slug, _currentUserService.UserId);

                // Increment view count asynchronously (fire and forget)
                _ = _blogService.IncrementViewCountAsync(blog.Id);

                return Ok(blog);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blog with slug '{slug}'");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the blog" });
            }
        }

        /// <summary>
        /// Creates a new blog
        /// </summary>
        /// <param name="request">Blog creation request</param>
        /// <returns>Created blog details</returns>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<BlogDetailResponse>> CreateBlog([FromBody] CreateBlogRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var blog = await _blogService.CreateBlogAsync(userId.Value, request);
                return CreatedAtAction(nameof(GetBlogById), new { id = blog.Id }, blog);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating blog");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating the blog" });
            }
        }

        /// <summary>
        /// Updates an existing blog
        /// </summary>
        /// <param name="id">Blog ID</param>
        /// <param name="request">Blog update request</param>
        /// <returns>Updated blog details</returns>
        [HttpPut("{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BlogDetailResponse>> UpdateBlog(Guid id, [FromBody] UpdateBlogRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var blog = await _blogService.UpdateBlogAsync(id, userId.Value, request);
                return Ok(blog);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating blog with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the blog" });
            }
        }

        /// <summary>
        /// Deletes a blog
        /// </summary>
        /// <param name="id">Blog ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteBlog(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _blogService.DeleteBlogAsync(id, userId.Value);
                return Ok(new { message = "Blog deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting blog with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while deleting the blog" });
            }
        }

        /// <summary>
        /// Likes a blog
        /// </summary>
        /// <param name="id">Blog ID</param>
        /// <returns>Success message</returns>
        [HttpPost("{id}/like")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LikeBlog(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _blogService.LikeBlogAsync(id, userId.Value);
                return Ok(new { message = "Blog liked successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error liking blog with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while liking the blog" });
            }
        }

        /// <summary>
        /// Unlikes a blog
        /// </summary>
        /// <param name="id">Blog ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("{id}/like")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UnlikeBlog(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _blogService.UnlikeBlogAsync(id, userId.Value);
                return Ok(new { message = "Blog unliked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unliking blog with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while unliking the blog" });
            }
        }

        /// <summary>
        /// Gets comments for a blog
        /// </summary>
        /// <param name="id">Blog ID</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of comments</returns>
        [HttpGet("{id}/comments")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedResponse<CommentResponse>>> GetComments(
            Guid id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 50) pageSize = 50;

                var comments = await _blogService.GetCommentsAsync(id, page, pageSize);
                return Ok(comments);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting comments for blog ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving comments" });
            }
        }

        /// <summary>
        /// Adds a comment to a blog
        /// </summary>
        /// <param name="id">Blog ID</param>
        /// <param name="request">Comment creation request</param>
        /// <returns>Created comment</returns>
        [HttpPost("{id}/comments")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CommentResponse>> AddComment(Guid id, [FromBody] CreateCommentRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var comment = await _blogService.AddCommentAsync(id, userId.Value, request);
                return Created($"/api/blogs/{id}/comments", comment);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding comment to blog ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while adding the comment" });
            }
        }

        /// <summary>
        /// Deletes a comment
        /// </summary>
        /// <param name="id">Blog ID</param>
        /// <param name="commentId">Comment ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("{id}/comments/{commentId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _blogService.DeleteCommentAsync(commentId, userId.Value);
                return Ok(new { message = "Comment deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting comment ID {commentId}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while deleting the comment" });
            }
        }

        /// <summary>
        /// Gets all categories
        /// </summary>
        /// <returns>List of categories</returns>
        [HttpGet("categories")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<List<CategoryResponse>>> GetCategories()
        {
            try
            {
                var categories = await _blogService.GetCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving categories" });
            }
        }

        /// <summary>
        /// Gets a specific category by ID
        /// </summary>
        /// <param name="id">Category ID</param>
        /// <returns>Category details</returns>
        [HttpGet("categories/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CategoryResponse>> GetCategoryById(Guid id)
        {
            try
            {
                var category = await _blogService.GetCategoryByIdAsync(id);
                return Ok(category);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting category with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the category" });
            }
        }

        /// <summary>
        /// Creates a new category
        /// </summary>
        /// <param name="request">Category creation request</param>
        /// <returns>Created category</returns>
        [HttpPost("categories")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<CategoryResponse>> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var category = await _blogService.CreateCategoryAsync(request, userId.Value);
                return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating the category" });
            }
        }

        /// <summary>
        /// Updates an existing category
        /// </summary>
        /// <param name="id">Category ID</param>
        /// <param name="request">Category update request</param>
        /// <returns>Updated category</returns>
        [HttpPut("categories/{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CategoryResponse>> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var category = await _blogService.UpdateCategoryAsync(id, request, userId.Value);
                return Ok(category);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating category with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the category" });
            }
        }

        /// <summary>
        /// Deletes a category
        /// </summary>
        /// <param name="id">Category ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("categories/{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _blogService.DeleteCategoryAsync(id, userId.Value);
                return Ok(new { message = "Category deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting category with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while deleting the category" });
            }
        }

        /// <summary>
        /// Searches blogs
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of matching blogs</returns>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResponse<BlogSummaryResponse>>> SearchBlogs(
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // Validate parameters
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { message = "Search query cannot be empty" });
                }

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 50) pageSize = 50;

                var blogs = await _blogService.SearchBlogsAsync(query, page, pageSize, _currentUserService.UserId);
                return Ok(blogs);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching blogs with query '{query}'");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while searching blogs" });
            }
        }
    }
}