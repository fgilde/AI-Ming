using static Core.GithubManager;

namespace Core;

public class GitHubRelease : IEquatable<GitHubRelease>
{
    public string TagName { get; set; }
    public List<Asset> Assets { get; set; }
    public string Description { get; set; }
    public string DownloadUrl => Assets?.FirstOrDefault(asset => asset.Name.EndsWith("zip"))?.DownloadUrl;

    public bool Equals(GitHubRelease? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return TagName == other.TagName;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((GitHubRelease)obj);
    }

    public override int GetHashCode()
    {
        return TagName.GetHashCode();
    }
}