using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{

    [ApiController]
    [Route("api/[controller]")]
    public class ChargeTypeService : IChargeTypeService
    {

        private readonly UserDbContext _context;

        public ChargeTypeService(UserDbContext context)
        {
            _context = context;
        }

        [HttpGet("Get/{id}")]
        public async Task<ChargeType> Get(int id)
        {
            var chargeType = await _context.ChargeTypes.Where(x => x.Id == id).FirstOrDefaultAsync();
            return chargeType;
        }

        [HttpGet("GetAll")]
        public async Task<List<ChargeType>> GetAll()
        {
            var chargeTypes = await _context.ChargeTypes.ToListAsync();
            return chargeTypes;
        }

        [HttpGet("GetByClassificationId/{classificationId}")]
        public async Task<List<ChargeType>> GetByClassificationId(int classificationId)
        {
            var chargeTypes = await _context.ChargeTypes.Where(x => x.ClassificationId == classificationId).ToListAsync();
            return chargeTypes;
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<ChargeType> SaveUpdate(int userId, ChargeType chargeType)
        {
            if (chargeType.Id == 0)
            {
                chargeType.CreatedBy = userId;
                chargeType.DateCreated = DateTime.Now;
                _context.ChargeTypes.Add(chargeType);

            }
            else
            {
                chargeType.UpdatedBy = userId;
                chargeType.DateUpdated = DateTime.Now;
                _context.ChargeTypes.Update(chargeType);
            }

            await _context.SaveChangesAsync();
            return chargeType;
        }
    }
}
