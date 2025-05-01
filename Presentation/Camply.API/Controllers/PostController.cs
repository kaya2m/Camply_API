// TheCamply.API/Controllers/PostController.cs
using System;
using System.Threading.Tasks;
using Camply.Application.Posts.DTOs;
using Camply.Application.Posts.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Camply.Domain.Common;
using Camply.Application.Common.Models;

namespace Camply.API.Controllers
{
    [ApiController]
    [Route("api/posts")]
    public class PostController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<PostController> _logger;

        public PostController(
            IPostService postService,
            ICurrentUserService currentUserService,
            ILogger<PostController> logger)
        {
            _postService = postService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Gets a paged list of posts
        /// </summary>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <param name="sortBy">Sort order (default: recent, options: recent, popular)</param>
        /// <returns>Paged list of posts</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> GetPosts(
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

                var posts = await _postService.GetPostsAsync(page, pageSize, sortBy, _currentUserService.UserId);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting posts");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving posts" });
            }
        }

        /// <summary>
        /// Gets a paged list of posts by a specific user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of posts by user</returns>
        [HttpGet("by-user/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> GetPostsByUser(
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

                var posts = await _postService.GetPostsByUserAsync(userId, page, pageSize, _currentUserService.UserId);
                return Ok(posts);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts for user ID {userId}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving posts" });
            }
        }

        /// <summary>
        /// Gets the current user's feed (posts from followed users)
        /// </summary>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of posts in the user's feed</returns>
        [HttpGet("feed")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> GetFeed(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 50) pageSize = 50;

                var posts = await _postService.GetFeedAsync(userId.Value, page, pageSize);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feed");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving feed" });
            }
        }

        /// <summary>
        /// Gets a paged list of posts with a specific tag
        /// </summary>
        /// <param name="tag">Tag name or slug</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of posts with tag</returns>
        [HttpGet("by-tag/{tag}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> GetPostsByTag(
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

                var posts = await _postService.GetPostsByTagAsync(tag, page, pageSize, _currentUserService.UserId);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts for tag '{tag}'");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving posts" });
            }
        }

        /// <summary>
        /// Gets a paged list of posts from a specific location
        /// </summary>
        /// <param name="locationId">Location ID</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of posts from location</returns>
        [HttpGet("by-location/{locationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> GetPostsByLocation(
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

                var posts = await _postService.GetPostsByLocationAsync(locationId, page, pageSize, _currentUserService.UserId);
                return Ok(posts);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts for location ID {locationId}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving posts" });
            }
        }

        /// <summary>
        /// Gets a specific post by ID
        /// </summary>
        /// <param name="id">Post ID</param>
        /// <returns>Post details</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PostDetailResponse>> GetPostById(Guid id)
        {
            try
            {
                var post = await _postService.GetPostByIdAsync(id, _currentUserService.UserId);
                return Ok(post);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting post with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the post" });
            }
        }

        /// <summary>
        /// Creates a new post
        /// </summary>
        /// <param name="request">Post creation request</param>
        /// <returns>Created post details</returns>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PostDetailResponse>> CreatePost([FromBody] CreatePostRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var post = await _postService.CreatePostAsync(userId.Value, request);
                return CreatedAtAction(nameof(GetPostById), new { id = post.Id }, post);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating the post" });
            }
        }

        /// <summary>
        /// Updates an existing post
        /// </summary>
        /// <param name="id">Post ID</param>
        /// <param name="request">Post update request</param>
        /// <returns>Updated post details</returns>
        [HttpPut("{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PostDetailResponse>> UpdatePost(Guid id, [FromBody] UpdatePostRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var post = await _postService.UpdatePostAsync(id, userId.Value, request);
                return Ok(post);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating post with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the post" });
            }
        }

        /// <summary>
        /// Deletes a post
        /// </summary>
        /// <param name="id">Post ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePost(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _postService.DeletePostAsync(id, userId.Value);
                return Ok(new { message = "Post deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting post with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while deleting the post" });
            }
        }

        /// <summary>
        /// Likes a post
        /// </summary>
        /// <param name="id">Post ID</param>
        /// <returns>Success message</returns>
        [HttpPost("{id}/like")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LikePost(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _postService.LikePostAsync(id, userId.Value);
                return Ok(new { message = "Post liked successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error liking post with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while liking the post" });
            }
        }

        /// <summary>
        /// Unlikes a post
        /// </summary>
        /// <param name="id">Post ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("{id}/like")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UnlikePost(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _postService.UnlikePostAsync(id, userId.Value);
                return Ok(new { message = "Post unliked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unliking post with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while unliking the post" });
            }
        }

        /// <summary>
        /// Gets comments for a post
        /// </summary>
        /// <param name="id">Post ID</param>
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

                var comments = await _postService.GetCommentsAsync(id, page, pageSize);
                return Ok(comments);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting comments for post ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving comments" });
            }
        }

        /// <summary>
        /// Adds a comment to a post
        /// </summary>
        /// <param name="id">Post ID</param>
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

                var comment = await _postService.AddCommentAsync(id, userId.Value, request);
                return Created($"/api/posts/{id}/comments", comment);
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
                _logger.LogError(ex, $"Error adding comment to post ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while adding the comment" });
            }
        }

        /// <summary>
        /// Deletes a comment
        /// </summary>
        /// <param name="id">Post ID</param>
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

                await _postService.DeleteCommentAsync(commentId, userId.Value);
                return Ok(new { message = "Comment deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
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
        /// Searches posts
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <returns>Paged list of matching posts</returns>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> SearchPosts(
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

                var posts = await _postService.SearchPostsAsync(query, page, pageSize, _currentUserService.UserId);
                return Ok(posts);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching posts with query '{query}'");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while searching posts" });
            }
        }
    }
}