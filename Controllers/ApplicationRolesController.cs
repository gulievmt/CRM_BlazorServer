using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CRMBlazorServerRBS.Models;

namespace CRMBlazorServerRBS.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Identity/ApplicationRoles")]
    public partial class ApplicationRolesController : ControllerBase
    {
       private readonly RoleManager<ApplicationRole> roleManager;

       public ApplicationRolesController(RoleManager<ApplicationRole> roleManager)
       {
           this.roleManager = roleManager;
       }

       partial void OnRolesRead(ref IQueryable<ApplicationRole> roles);

       [HttpGet]
       public IEnumerable<ApplicationRole> Get()
       {
           var roles = roleManager.Roles;
           OnRolesRead(ref roles);

           return roles;
       }

       partial void OnRoleCreated(ApplicationRole role);

       [HttpPost]
       public async Task<IActionResult> Post([FromBody] ApplicationRole role)
       {
           if (role == null)
           {
               return BadRequest();
           }

           OnRoleCreated(role);

           var result = await roleManager.CreateAsync(role);

           if (!result.Succeeded)
           {
               var message = string.Join(", ", result.Errors.Select(error => error.Description));

               return BadRequest(new { error = new { message }});
           }

           return Created($"api/Identity/ApplicationRoles/{role.Id}", role);
       }

       partial void OnRoleDeleted(ApplicationRole role);

       [HttpDelete("{id}")]
       public async Task<IActionResult> Delete(string id)
       {
           var role = await roleManager.FindByIdAsync(id);

           if (role == null)
           {
               return NotFound();
           }

           OnRoleDeleted(role);

           var result = await roleManager.DeleteAsync(role);

           if (!result.Succeeded)
           {
               var message = string.Join(", ", result.Errors.Select(error => error.Description));

               return BadRequest(new { error = new { message }});
           }

           return new NoContentResult();
       }
    }
}
