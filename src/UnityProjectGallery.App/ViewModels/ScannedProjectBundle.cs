using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>一次扫描中，单个工程的结构信息与素材索引结果。</summary>
public sealed record ScannedProjectBundle(
    UnityProject Project,
    ProjectMediaScanResult MediaResult);
