using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using VTech.AsyncRefactoring.Base.Changes;

namespace VTech.AsyncRefactoring.VisualStudio.Extension.ChangesPreview;

public class ChangesPreviewContextViewModel
{
    public ChangesPreviewContextViewModel(List<ProjectChanges> changes)
    {
        Changes = new(changes.Select(x => new ProjectChangesViewModel(x)));
    }

    public ObservableCollection<ProjectChangesViewModel> Changes { get; private set; } = [];

    public List<ProjectChanges> GetSelectedChanges()
    {
        if (Changes.All(x => !x.HasSelectedChanges))
        {
            return [];
        }

        List<ProjectChanges> result = [];

        foreach (var change in Changes.Where(x => x.HasSelectedChanges))
        {
            ProjectChanges projectChanges = new()
            {
                Id = change.Id
            };

            foreach (var doc in change.Documents.Where(x => x.HasSelectedChanges))
            {
                DocumentChanges documentChanges = new()
                {
                    Id = doc.Id,
                    TextChanges = []
                };

                foreach (var textChnage in doc.TextChanges.Where(x => x.IsSelected))
                {
                    documentChanges.TextChanges.Add(textChnage.TextChange);
                }

                projectChanges.Documents.Add(documentChanges);
            }

            result.Add(projectChanges);
        }

        return result;
    }
}
