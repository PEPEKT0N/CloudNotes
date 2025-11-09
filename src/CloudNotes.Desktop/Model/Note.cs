using System;
using System.Collections.Generic;

namespace CloudNotes.Models;


public partial class Note
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
