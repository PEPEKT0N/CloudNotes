using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using CloudNotes.Desktop.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CloudNotes.Desktop.Tests;

public abstract class NoteServiceTestsBase : IDisposable
{
    protected readonly AppDbContext _context;
    protected readonly NoteService _service;

    public NoteServiceTestsBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // Каждый тест получает свою базу данных
            .Options;

        _context = new AppDbContext(options);
        // Передаём _context в конструктор NoteService
        _service = new NoteService(_context);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
