using Crypto_Payment.DTOS;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[ApiController]
[Authorize]
[Route("/api/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;

    public CustomersController(ICustomerService service)
    {
        _service = service;
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
            return StatusCode(500, new { title = "Sunucu hatası", detail = ex.Message });
        }
    }

    // GET BY ID
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var customer = await _service.GetByIdAsync(id);
            return Ok(customer);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { title = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { title = "Sunucu hatası", detail = ex.Message });
        }
    }
    
    
    // CREATE
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CustomerDto dto)
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
            return StatusCode(500, new { title = "Sunucu hatası", detail = ex.Message });
        }
    }

    // UPDATE
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerDto dto)
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
            return StatusCode(500, new { title = "Sunucu hatası", detail = ex.Message });
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
            return StatusCode(500, new { title = "Sunucu hatası", detail = ex.Message });
        }
    }
}