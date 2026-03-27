using CommendBod.Model;
using Microsoft.EntityFrameworkCore;

namespace CommendBot.Data;

public class CommendRepository
{
    private readonly CommendBotDbContext _context;

    public CommendRepository(CommendBotDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new commend record and returns it with the generated Id.
    /// </summary>
    public async Task<Commend> CreateAsync(Commend commend)
    {
        _context.Commends.Add(commend);
        await _context.SaveChangesAsync();
        return commend;
    }

    /// <summary>
    /// Gets a commend by its primary key.
    /// </summary>
    public async Task<Commend?> GetByIdAsync(int id)
    {
        return await _context.Commends.FindAsync(id);
    }

    /// <summary>
    /// Gets all commends for a given user (the user who was commended).
    /// </summary>
    public async Task<List<Commend>> GetByUserIdAsync(ulong userId)
    {
        return await _context.Commends
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all commends sent by a specific user.
    /// </summary>
    public async Task<List<Commend>> GetBySentByUserIdAsync(ulong sentByUserId)
    {
        return await _context.Commends
            .Where(c => c.SentByUserId == sentByUserId)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all commends.
    /// </summary>
    public async Task<List<Commend>> GetAllAsync()
    {
        return await _context.Commends
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();
    }

    /// <summary>
    /// Updates an existing commend record.
    /// </summary>
    public async Task<Commend?> UpdateAsync(Commend commend)
    {
        var existing = await _context.Commends.FindAsync(commend.Id);
        if (existing is null)
        {
            return null;
        }
        
        existing.UserId = commend.UserId;
        existing.Reason = commend.Reason;
        existing.SentByUserId = commend.SentByUserId;
        existing.CreatedDate = commend.CreatedDate;

        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Deletes a commend by its primary key. Returns true if found and deleted.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _context.Commends.FindAsync(id);
        if (existing is null)
        {
            return false;
        }

        _context.Commends.Remove(existing);
        await _context.SaveChangesAsync();
        return true;
    }
}
