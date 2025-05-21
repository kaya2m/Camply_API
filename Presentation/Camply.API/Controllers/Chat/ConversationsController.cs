using Camply.Application.Messages.DTOs;
using Camply.Application.Messages.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Camply.API.Controllers.Chat
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        private readonly IMessageService _messageService;
        private readonly ILogger<ConversationsController> _logger;

        public ConversationsController(
            IConversationService conversationService,
            IMessageService messageService,
            ILogger<ConversationsController> logger)
        {
            _conversationService = conversationService;
            _messageService = messageService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConversationDto>>> GetConversations(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetUserId();
                var conversations = await _conversationService.GetUserConversationsAsync(userId, page, pageSize);
                return Ok(conversations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ConversationDto>> GetConversation(string id)
        {
            try
            {
                var userId = GetUserId();
                var conversation = await _conversationService.GetConversationByIdAsync(id, userId);

                if (conversation == null)
                {
                    return NotFound();
                }

                return Ok(conversation);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting conversation {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<ConversationDto>> CreateConversation(CreateConversationDto createConversationDto)
        {
            try
            {
                var userId = GetUserId();
                var conversation = await _conversationService.CreateConversationAsync(createConversationDto, userId);

                return CreatedAtAction(nameof(GetConversation), new { id = conversation.Id }, conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("direct/{userId}")]
        public async Task<ActionResult<ConversationDto>> CreateDirectConversation(string userId)
        {
            try
            {
                var currentUserId = GetUserId();

                if (currentUserId == userId)
                {
                    return BadRequest("Cannot create conversation with yourself");
                }

                var conversation = await _conversationService.GetOrCreateDirectConversationAsync(currentUserId, userId);

                return CreatedAtAction(nameof(GetConversation), new { id = conversation.Id }, conversation);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating direct conversation with user {userId}"); _logger.LogError(ex, $"Error creating direct conversation with user {userId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/mute")]
        public async Task<ActionResult> MuteConversation(string id, [FromQuery] bool mute)
        {
            try
            {
                var userId = GetUserId();
                await _conversationService.MuteConversationAsync(id, userId, mute);

                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error {(mute ? "muting" : "unmuting")} conversation {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/archive")]
        public async Task<ActionResult> ArchiveConversation(string id)
        {
            try
            {
                var userId = GetUserId();
                await _conversationService.ArchiveConversationAsync(id, userId);

                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error archiving conversation {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteConversation(string id)
        {
            try
            {
                var userId = GetUserId();
                await _conversationService.DeleteConversationAsync(id, userId);

                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting conversation {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/messages")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetConversationMessages(
            string id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = GetUserId();
                var messages = await _messageService.GetConversationMessagesAsync(id, userId, page, pageSize);

                return Ok(messages);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting messages for conversation {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/media")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetConversationMedia(
            string id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetUserId();
                var mediaMessages = await _messageService.GetMediaMessagesAsync(id, userId, page, pageSize);

                return Ok(mediaMessages);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting media for conversation {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/search")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> SearchConversation(
            string id,
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Search query cannot be empty");
                }

                var userId = GetUserId();
                var messages = await _messageService.SearchMessagesAsync(id, userId, query, page, pageSize);

                return Ok(messages);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching messages in conversation {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/read")]
        public async Task<ActionResult> MarkConversationAsRead(string id)
        {
            try
            {
                var userId = GetUserId();
                await _messageService.MarkConversationAsReadAsync(id, userId);

                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking conversation {id} as read");
                return StatusCode(500, "Internal server error");
            }
        }

        private string GetUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("User not authenticated");
        }
    }
}