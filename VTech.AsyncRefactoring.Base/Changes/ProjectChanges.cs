﻿namespace VTech.AsyncRefactoring.Base.Changes;

public class ProjectChanges
{
    public string Id { get; set; }
    public List<DocumentChanges> Documents { get; } = [];
}

