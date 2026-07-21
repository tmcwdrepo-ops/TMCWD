using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Utility.Generic;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ÌnpectionTypeDetailController : Controller
    {

        [HttpPost("SaveUpdate")]
        public ActionResult<bool> SaveUpdate([FromBody] InspectionTypeDetail inspectionTypeDetail)
        {
            try
            {
                using (var dbContext = new UserDbContext())
                {
                    if(inspectionTypeDetail.Id == 0)
                    {
                        dbContext.InspectonTypeDetails.Add(inspectionTypeDetail);
                    }
                    else
                    {
                        dbContext.InspectonTypeDetails.Update(inspectionTypeDetail);
                    }

                    int res = dbContext.SaveChanges();
                    if (res > 0) return Ok(true);
                } 
            }
            catch(Exception ex)
            {
                Logger.Log(ErrorModule.Data, ErrorType.Error, ex.Message);
                return Problem(ex.Message, ErrorModule.Data.ToString(), StatusCodes.Status500InternalServerError, ErrorType.Error.ToString(), ErrorType.Error.ToString());
            }

            return Ok(false);
        }

        [HttpGet("GetDetail")]
        public ActionResult<InspectionTypeDetail> GetDetail(int inspectionTypeId)
        {
            InspectionTypeDetail detail = new();

            try
            {
                using(var dbContext = new UserDbContext())
                {
                    detail = dbContext.InspectonTypeDetails.Where(x => x.Id == inspectionTypeId).FirstOrDefault() ?? new InspectionTypeDetail();
                    if (detail == null) return NotFound($"Inspection type detail with inspection type id {inspectionTypeId} not found.");
                }
            }
            catch(Exception ex)
            {
                Logger.Log(ErrorModule.Data, ErrorType.Error, ex.Message);
                return Problem(ex.Message, ErrorModule.Data.ToString(), StatusCodes.Status500InternalServerError, ErrorType.Error.ToString(), ErrorType.Error.ToString());
            }

            return Ok(detail);
        }

    }
}
