using Crypto_Payment.DTOS;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[ApiController]
[Authorize]
[Route("/api/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _service;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(IPermissionService service, ILogger<PermissionsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // LIST
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAll");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    // GET BY ID
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var permission = await _service.GetByIdAsync(id);
            return Ok(permission);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { title = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetById");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }
    
    
    // CREATE
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] PermissionDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(dto);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Create");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }

    // UPDATE
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PermissionDto dto)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { title = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Update");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }
    
    // DELETE
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { title = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Delete");
            return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
        }
    }
}
