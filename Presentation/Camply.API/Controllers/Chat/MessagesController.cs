using Camply.Application.Messages.DTOs;
using Camply.Application.Messages.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Camply.API.Controllers.Chat
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly IReactionService _reactionService;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(
            IMessageService messageService,
            IReactionService reactionService,
            ILogger<MessagesController> logger)
        {
            _messageService = messageService;
            _reactionService = reactionService;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MessageDto>> GetMessage(string id)
        {
            try
            {
                var userId = GetUserId();
                var message = await _messageService.GetMessageByIdAsync(id, userId);

                if (message == null)
                {
                    return NotFound();
                }

                return Ok(message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting message {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<MessageDto>> SendMessage(SendMessageDto sendMessageDto)
        {
            try
            {
                var userId = GetUserId();
                var message = await _messageService.SendMessageAsync(sendMessageDto, userId);

                return CreatedAtAction(nameof(GetMessage), new { id = message.Id }, message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<MessageDto>> EditMessage(string id, [FromBody] string newContent)
        {
            try
            {
                var userId = GetUserId();
                var message = await _messageService.EditMessageAsync(id, userId, newContent);

                return Ok(message);
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
                _logger.LogError(ex, $"Error editing message {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteMessage(string id)
        {
            try
            {
                var userId = GetUserId();
                await _messageService.DeleteMessageAsync(id, userId);

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
                _logger.LogError(ex, $"Error deleting message {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/read")]
        public async Task<ActionResult> MarkAsRead(string id)
        {
            try
            {
                var userId = GetUserId();
                await _messageService.MarkAsReadAsync(id, userId);

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
                _logger.LogError(ex, $"Error marking message {id} as read");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/like")]
        public async Task<ActionResult<MessageDto>> ToggleLike(string id)
        {
            try
            {
                var userId = GetUserId();
                var message = await _messageService.ToggleLikeMessageAsync(id, userId);

                return Ok(message);
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
                _logger.LogError(ex, $"Error toggling like for message {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/save")]
        public async Task<ActionResult<MessageDto>> ToggleSave(string id)
        {
            try
            {
                var userId = GetUserId();
                var message = await _messageService.ToggleSaveMessageAsync(id, userId);

                return Ok(message);
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
                _logger.LogError(ex, $"Error toggling save for message {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/reactions")]
        public async Task<ActionResult<ReactionDto>> AddReaction(string id, [FromBody] string reactionType)
        {
            try
            {
                var userId = GetUserId();
                var reaction = await _reactionService.AddReactionAsync(new AddReactionDto
                {
                    MessageId = id,
                    ReactionType = reactionType
                }, userId);

                return Ok(reaction);
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
                _logger.LogError(ex, $"Error adding reaction to message {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}/reactions")]
        public async Task<ActionResult> RemoveReaction(string id)
        {
            try
            {
                var userId = GetUserId();
                await _reactionService.RemoveReactionAsync(id, userId);

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
                _logger.LogError(ex, $"Error removing reaction from message {id}");
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