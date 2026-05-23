using System.IO;
using UnityEngine;
using UnityEditor;

namespace ParticleMeshTools
{
    /// <summary>
    /// 材质复制工具类
    /// </summary>
    public static class MaterialCopyUtility
    {
        /// <summary>
        /// 复制材质到指定文件夹
        /// </summary>
        /// <param name="sourceMaterial">源材质</param>
        /// <param name="targetFolderPath">目标文件夹路径（相对于Assets）</param>
        /// <param name="newName">新材质名称（可选，为空则使用原名称）</param>
        /// <returns>复制后的材质</returns>
        public static Material CopyMaterialToFolder(Material sourceMaterial, string targetFolderPath, string newName = "")
        {
            if (sourceMaterial == null)
            {
                Debug.LogError("源材质为空，无法复制");
                return null;
            }
            
            // 确保目标文件夹路径以Assets开头
            if (!targetFolderPath.StartsWith("Assets"))
            {
                targetFolderPath = "Assets/" + targetFolderPath;
            }
            
            // 移除末尾的斜杠
            targetFolderPath = targetFolderPath.TrimEnd('/', '\\');
            
            // 确保目标文件夹存在
            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                // 创建文件夹
                CreateFolderRecursive(targetFolderPath);
            }
            
            // 确定新材质的名称
            string materialName = string.IsNullOrEmpty(newName) ? sourceMaterial.name : newName;
            
            // 生成唯一的文件名
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{targetFolderPath}/{materialName}.mat");
            
            // 创建新材质
            Material newMaterial = new Material(sourceMaterial);
            
            // 复制所有属性
            CopyMaterialProperties(sourceMaterial, newMaterial);
            
            // 保存材质
            AssetDatabase.CreateAsset(newMaterial, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"材质已复制: {sourceMaterial.name} -> {targetPath}");
            
            return newMaterial;
        }
        
        /// <summary>
        /// 复制材质的所有属性
        /// </summary>
        private static void CopyMaterialProperties(Material source, Material target)
        {
            // 复制Shader
            target.shader = source.shader;
            
            // 复制所有属性
            target.CopyPropertiesFromMaterial(source);
            
            // 复制渲染队列
            target.renderQueue = source.renderQueue;
            
            // 复制关键字
            target.shaderKeywords = source.shaderKeywords;
            
            // 复制全局光照标志
            target.globalIlluminationFlags = source.globalIlluminationFlags;
            
            // 复制双面渲染设置
            target.doubleSidedGI = source.doubleSidedGI;
            
            // 复制启用实例化
            target.enableInstancing = source.enableInstancing;
        }

        public static Texture TryGetPrimaryTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_MainTex"))
            {
                return material.GetTexture("_MainTex");
            }

            if (material.HasProperty("BaseMap"))
            {
                return material.GetTexture("BaseMap");
            }

            if (material.HasProperty("_BaseMap"))
            {
                return material.GetTexture("_BaseMap");
            }

            if (material.HasProperty("_BaseColorMap"))
            {
                return material.GetTexture("_BaseColorMap");
            }

            if (material.HasProperty("_AlbedoTex"))
            {
                return material.GetTexture("_AlbedoTex");
            }

            return null;
        }
        
        /// <summary>
        /// 递归创建文件夹
        /// </summary>
        private static void CreateFolderRecursive(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;
            
            string parentFolder = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            
            // 递归创建父文件夹
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                CreateFolderRecursive(parentFolder);
            }
            
            // 创建当前文件夹
            AssetDatabase.CreateFolder(parentFolder, folderName);
        }

        public static Texture[] CopyAllTexturesFromMaterial(Material material, string targetFolderPath)
        {
            if (material == null)
            {
                return System.Array.Empty<Texture>();
            }

            var serializedMaterial = new SerializedObject(material);
            var propertyIterator = serializedMaterial.GetIterator();
            var copiedTextures = new System.Collections.Generic.List<Texture>();
            bool hasProperty = propertyIterator.NextVisible(true);

            while (hasProperty)
            {
                if (propertyIterator.propertyType == SerializedPropertyType.ObjectReference && propertyIterator.objectReferenceValue is Texture texture && texture != null)
                {
                    var copiedTexture = CopyTextureToFolder(texture, targetFolderPath);
                    if (copiedTexture != null)
                    {
                        copiedTextures.Add(copiedTexture);
                    }
                }

                hasProperty = propertyIterator.NextVisible(false);
            }

            return copiedTextures.ToArray();
        }
        
        /// <summary>
        /// 从Project窗口获取选中的文件夹路径
        /// </summary>
        public static string GetSelectedFolderPath()
        {
            string path = "Assets";
            
            foreach (var obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                }
                break;
            }
            
            return path;
        }
        
        /// <summary>
        /// 验证文件夹路径是否有效
        /// </summary>
        public static bool IsValidFolderPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return false;
            
            // 确保路径以Assets开头
            if (!folderPath.StartsWith("Assets"))
                folderPath = "Assets/" + folderPath;
            
            return AssetDatabase.IsValidFolder(folderPath);
        }
        
        /// <summary>
        /// 批量复制材质
        /// </summary>
        public static Material[] CopyMaterials(Material[] sourceMaterials, string targetFolderPath)
        {
            if (sourceMaterials == null || sourceMaterials.Length == 0)
                return new Material[0];
            
            Material[] copiedMaterials = new Material[sourceMaterials.Length];
            
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                if (sourceMaterials[i] != null)
                {
                    copiedMaterials[i] = CopyMaterialToFolder(sourceMaterials[i], targetFolderPath);
                }
            }
            
            return copiedMaterials;
        }
        
        /// <summary>
        /// 复制贴图到指定文件夹
        /// </summary>
        public static Texture CopyTextureToFolder(Texture sourceTexture, string targetFolderPath, string newName = "")
        {
            if (sourceTexture == null)
            {
                Debug.LogError("源贴图为空，无法复制");
                return null;
            }
            
            // 获取源贴图路径
            string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("无法获取源贴图路径");
                return null;
            }
            
            // 确保目标文件夹路径以Assets开头
            if (!targetFolderPath.StartsWith("Assets"))
            {
                targetFolderPath = "Assets/" + targetFolderPath;
            }
            
            // 移除末尾的斜杠
            targetFolderPath = targetFolderPath.TrimEnd('/', '\\');
            
            // 确保目标文件夹存在
            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                CreateFolderRecursive(targetFolderPath);
            }
            
            // 确定新贴图的名称
            string textureName = string.IsNullOrEmpty(newName) ? sourceTexture.name : newName;
            string extension = Path.GetExtension(sourcePath);
            
            // 生成唯一的文件名
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{targetFolderPath}/{textureName}{extension}");
            
            // 复制文件
            AssetDatabase.CopyAsset(sourcePath, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // 加载复制后的贴图
            Texture copiedTexture = AssetDatabase.LoadAssetAtPath<Texture>(targetPath);
            
            Debug.Log($"贴图已复制: {sourceTexture.name} -> {targetPath}");
            
            return copiedTexture;
        }
        
        /// <summary>
        /// 复制Mesh到指定文件夹
        /// </summary>
        /// <param name="sourceMesh">源Mesh</param>
        /// <param name="targetFolderPath">目标文件夹路径（相对于Assets）</param>
        /// <param name="newName">新Mesh名称（可选，为空则使用原名称）</param>
        /// <returns>复制后的Mesh</returns>
        public static Mesh CopyMeshToFolder(Mesh sourceMesh, string targetFolderPath, string newName = "")
        {
            if (sourceMesh == null)
            {
                Debug.LogError("源Mesh为空，无法复制");
                return null;
            }
            
            // 获取源Mesh路径
            string sourcePath = AssetDatabase.GetAssetPath(sourceMesh);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError($"无法获取Mesh资源路径: {sourceMesh.name}");
                return null;
            }
            
            // 确保目标文件夹路径以Assets开头
            if (!targetFolderPath.StartsWith("Assets"))
            {
                targetFolderPath = "Assets/" + targetFolderPath;
            }
            
            // 移除末尾的斜杠
            targetFolderPath = targetFolderPath.TrimEnd('/', '\\');
            
            // 确保目标文件夹存在
            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                CreateFolderRecursive(targetFolderPath);
            }
            
            // 确定新Mesh的名称
            string meshName = string.IsNullOrEmpty(newName) ? sourceMesh.name : newName;
            string extension = Path.GetExtension(sourcePath);
            
            // 如果没有扩展名，默认使用.asset
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".asset";
            }
            
            // 生成唯一的文件名
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{targetFolderPath}/{meshName}{extension}");
            
            // 复制文件
            bool copySuccess = AssetDatabase.CopyAsset(sourcePath, targetPath);
            
            if (!copySuccess)
            {
                Debug.LogError($"复制Mesh失败: {sourcePath} -> {targetPath}");
                return null;
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // 加载复制后的Mesh
            Mesh copiedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath);
            
            if (copiedMesh == null)
            {
                Debug.LogError($"加载复制后的Mesh失败: {targetPath}");
                return null;
            }
            
            Debug.Log($"Mesh已复制: {sourceMesh.name} -> {targetPath}");
            
            return copiedMesh;
        }
        
        /// <summary>
        /// 批量复制Mesh到指定文件夹
        /// </summary>
        public static Mesh[] CopyMeshes(Mesh[] sourceMeshes, string targetFolderPath)
        {
            if (sourceMeshes == null || sourceMeshes.Length == 0)
            {
                Debug.LogWarning("源Mesh数组为空");
                return null;
            }
            
            Mesh[] copiedMeshes = new Mesh[sourceMeshes.Length];
            
            for (int i = 0; i < sourceMeshes.Length; i++)
            {
                if (sourceMeshes[i] != null)
                {
                    copiedMeshes[i] = CopyMeshToFolder(sourceMeshes[i], targetFolderPath);
                }
            }
            
            return copiedMeshes;
        }
    }
}
