namespace Aiursoft.GitRunner.Models;

public enum CloneMode
{
    /// <summary>
    /// git clone --filter=blob:none
    /// 
    /// creates a blobless clone. These clones download all reachable commits and trees, while fetching blobs on-demand. 
    /// 
    /// These clones are best for developers and build environments that span multiple builds.
    /// </summary>
    CommitsAndTrees,

    /// <summary>
    /// git clone --filter=tree:0
    /// 
    /// creates a treeless clone. These clones download all reachable commits, while fetching trees and blobs on-demand. 
    /// 
    /// These clones are best for build environments where the repository will be deleted after a single build, but you still need access to commit history.
    /// </summary>
    OnlyCommits,

    /// <summary>
    /// git clone --depth=1
    /// 
    /// creates a shallow clone. These clones truncate the commit history to reduce the clone size. 
    /// 
    /// This creates some unexpected behavior issues, limiting which Git commands are possible. These clones also put undue stress on later fetches, so they are strongly discouraged for developer use. They are helpful for some build environments where the repository will be deleted after a single build.
    /// </summary>
    Depth1,

    /// <summary>
    /// git clone
    /// 
    /// creates a full clone. These clones download all reachable commits, trees, and blobs.
    /// 
    /// These clones are best for developers to build and inspect file history.
    /// </summary>
    Full,
    
    /// <summary>
    /// git clone --bare
    ///
    /// creates a bare clone. These clones download all reachable commits, trees, and blobs, but omit checking out any files.
    ///
    /// These clones are best for server environments where you don't need a working copy of the code. They are also helpful for build environments where the repository will be deleted after a single build.
    /// </summary>
    Bare
}
