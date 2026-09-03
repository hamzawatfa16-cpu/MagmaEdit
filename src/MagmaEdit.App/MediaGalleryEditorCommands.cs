using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.App;

/// <summary>Adapts media-gallery state changes to the shared editor command gateway.</summary>
internal sealed class MediaGalleryEditorCommands
{
    private readonly Func<ProjectDocument> _getProject;
    private readonly Action _saveProject;

    public MediaGalleryEditorCommands(
        Func<ProjectDocument> getProject,
        Action saveProject)
    {
        ArgumentNullException.ThrowIfNull(getProject);
        ArgumentNullException.ThrowIfNull(saveProject);
        _getProject = getProject;
        _saveProject = saveProject;
    }

    public bool SetPublished(MediaAsset asset, bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(asset);

        ProjectDocument project = _getProject();
        bool exists = project.Media.Any(item =>
            string.Equals(item.Id, asset.Id, StringComparison.Ordinal));
        if (!exists)
        {
            return false;
        }

        try
        {
            new EditorCommandGateway(project).SetMediaPublished(asset.Id, isPublished);
            _saveProject();
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
