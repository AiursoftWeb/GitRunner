using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.GitRunner.Models;

public enum CloneMode
{
    /// <summary>
    /// git clone --filter=blob:none <url> 
    /// 
    /// creates a blobless clone. These clones download all reachable commits and trees, while fetching blobs on-demand. 
    /// 
    /// These clones are best for developers and build environments that span multiple builds.
    /// </summary>
    CommitsAndTrees,

    /// <summary>
    /// git clone --filter=tree:0 <url>
    /// 
    /// creates a treeless clone. These clones download all reachable commits, while fetching trees and blobs on-demand. 
    /// 
    /// These clones are best for build environments where the repository will be deleted after a single build, but you still need access to commit history.
    /// </summary>
    OnlyCommits,

    /// <summary>
    /// git clone --depth=1 <url>
    /// 
    /// creates a shallow clone. These clones truncate the commit history to reduce the clone size. 
    /// 
    /// This creates some unexpected behavior issues, limiting which Git commands are possible. These clones also put undue stress on later fetches, so they are strongly discouraged for developer use. They are helpful for some build environments where the repository will be deleted after a single build.
    /// </summary>
    Depth1,

    /// <summary>
    /// git clone <url>
    /// 
    /// creates a full clone. These clones download all reachable commits, trees, and blobs.
    /// 
    /// These clones are best for developers to build and inspect file history.
    /// </summary>
    Full
}
