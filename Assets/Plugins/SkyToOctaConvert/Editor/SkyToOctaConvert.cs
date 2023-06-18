using System.IO;

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class SkyToOctaConvert : EditorWindow
{
	public enum SkyBoxSource_Type
	{
		CubeMap,
		LatLon,
		LatLonHalf
	}
	public enum SkyBoxOutput_Type
	{
		HalfOcta,
		FullOcta
	}


	[MenuItem("Tools/TwoPolygons/Convert SkyBox To Octa")]
	public static void OpenWindow()
	{
		EditorWindow.GetWindow<SkyToOctaConvert>(true, "SkyBox To Octa Convert");
	}

	[HideInInspector] public Shader octaShader;

	private Texture skyBoxSource;
	private SkyBoxSource_Type skyboxSource_Type = SkyBoxSource_Type.CubeMap;
	private bool autoDetectType = true;
	
	private SkyBoxOutput_Type skyboxOutput_Type = SkyBoxOutput_Type.HalfOcta;

	private int   size = 1024;

	private bool  hdr = true;
	private bool  tonemap = false;
	private float exposure = 0.0f;
	private float tonemapShoulder = 1.06f;

	private bool  blueNoise = false;
	private float blueNoiseIntensity = 0.5f;

	private Material mat;

	private bool allowToProcess = true;
	private void OnGUI()
	{
		allowToProcess = true;

		skyBoxSource = (Texture)EditorGUILayout.ObjectField("Source Texture", skyBoxSource, typeof(Texture), false);
		if(!skyBoxSource)
		{
			allowToProcess = false;
			EditorGUILayout.HelpBox("Select a sky texture", MessageType.Warning);
		}

		EditorGUILayout.Space();
		EditorGUI.BeginDisabledGroup(autoDetectType == true);
			skyboxSource_Type = (SkyBoxSource_Type)EditorGUILayout.EnumPopup("Source Texture Type", skyboxSource_Type);

		EditorGUI.EndDisabledGroup();


		EditorGUI.indentLevel++;
			autoDetectType = EditorGUILayout.Toggle("Auto Detect", autoDetectType);

			if(skyBoxSource && autoDetectType)
			{
				if(skyBoxSource.dimension == TextureDimension.Cube)
					skyboxSource_Type = SkyBoxSource_Type.CubeMap;

				if(skyBoxSource.dimension == TextureDimension.Tex2D)
				{
					skyboxSource_Type = SkyBoxSource_Type.LatLon;

					if(skyBoxSource.width > skyBoxSource.height * 3)
						skyboxSource_Type = SkyBoxSource_Type.LatLonHalf;
				}
			}
		EditorGUI.indentLevel--;

		if(skyBoxSource && !autoDetectType)
		{
			if( skyboxSource_Type == SkyBoxSource_Type.CubeMap    && skyBoxSource.dimension != TextureDimension.Cube  ||
				skyboxSource_Type == SkyBoxSource_Type.LatLon     && skyBoxSource.dimension != TextureDimension.Tex2D ||
				skyboxSource_Type == SkyBoxSource_Type.LatLonHalf && skyBoxSource.dimension != TextureDimension.Tex2D
				)
			{
				allowToProcess = false;
				EditorGUILayout.HelpBox("SkyBox type and selected type are not compatible", MessageType.Warning);
			}
		}

		EditorGUILayout.Space();
		skyboxOutput_Type = (SkyBoxOutput_Type)EditorGUILayout.EnumPopup("Output Texture Type", skyboxOutput_Type);
		if(skyboxSource_Type == SkyBoxSource_Type.LatLonHalf && skyboxOutput_Type == SkyBoxOutput_Type.FullOcta)
		{
			allowToProcess = false;
			EditorGUILayout.HelpBox("Half sized SkyBox can not be converted to Full sized Octa", MessageType.Warning);
		}

		EditorGUILayout.Space();
		hdr = EditorGUILayout.Toggle("Save As HDR", hdr);
		
		EditorGUILayout.Space();
		EditorGUI.BeginDisabledGroup(hdr == true);
			tonemap = EditorGUILayout.Toggle("Tonemaping", tonemap);

			EditorGUI.indentLevel++;
				EditorGUI.BeginDisabledGroup(tonemap == false);
					exposure = EditorGUILayout.Slider("Exposure", exposure, -10.0f, 10.0f);
					tonemapShoulder = EditorGUILayout.Slider("Tone Map Shoulder", tonemapShoulder, 0.25f, 4.0f);
				EditorGUI.EndDisabledGroup();
			EditorGUI.indentLevel--;

			EditorGUILayout.Space();
			blueNoise = EditorGUILayout.Toggle("Add Blue Noise", blueNoise);

			EditorGUI.indentLevel++;
				EditorGUI.BeginDisabledGroup(blueNoise == false);
					blueNoiseIntensity = EditorGUILayout.Slider("Blue Noise Intensity", blueNoiseIntensity, 0.0f, 1.0f);
				EditorGUI.EndDisabledGroup();
			EditorGUI.indentLevel--;
		EditorGUI.EndDisabledGroup();


		EditorGUILayout.Space();
		size = EditorGUILayout.IntSlider("Output Texture Size", size, 128, 4096);
		size = Mathf.ClosestPowerOfTwo(size);


		EditorGUILayout.Space();
		EditorGUI.BeginDisabledGroup(allowToProcess == false);
			if(GUILayout.Button("Save PNG file"))
			{
				if(mat == null)
					mat = new Material(octaShader);

				SetUniforms();

				//Convert to Octa. Use supersampling
				int size2x = Mathf.Min(size * 2, SystemInfo.maxTextureSize);
				RenderTexture outTexture_tmp = RenderTexture.GetTemporary(size2x, size2x, 0, RenderTextureFormat.ARGBHalf);
				Graphics.Blit(skyBoxSource, outTexture_tmp, mat, 0);

				//Resolve supersampled
				RenderTexture outTexture = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBHalf);
				Graphics.Blit(outTexture_tmp, outTexture, mat, 1);

				SaveTexture(outTexture);

				RenderTexture.ReleaseTemporary(outTexture_tmp);
				RenderTexture.ReleaseTemporary(outTexture);
			}
		EditorGUI.EndDisabledGroup();
	}


	private void SetUniforms()
	{
		mat.DisableKeyword("_SOURCE_CUBE");
		mat.DisableKeyword("_SOURCE_LATLON");
		mat.DisableKeyword("_SOURCE_LATLON_HALF");

		if(skyboxSource_Type == SkyBoxSource_Type.CubeMap)
		{
			mat.SetTexture("_CubeMap", skyBoxSource);
			mat.EnableKeyword("_SOURCE_CUBE");
		} else if(skyboxSource_Type == SkyBoxSource_Type.LatLon)
		{
			mat.SetTexture("_MainTex", skyBoxSource);
			mat.EnableKeyword("_SOURCE_LATLON");
		} else if(skyboxSource_Type == SkyBoxSource_Type.LatLonHalf)
		{
			mat.SetTexture("_MainTex", skyBoxSource);
			mat.EnableKeyword("_SOURCE_LATLON_HALF");
		}

		if(skyboxOutput_Type == SkyBoxOutput_Type.HalfOcta)
		{
			SetKeyword(mat, "_OUT_HALF", true);
			SetKeyword(mat, "_OUT_FULL", false);
		} else if(skyboxOutput_Type == SkyBoxOutput_Type.FullOcta)
		{
			SetKeyword(mat, "_OUT_HALF", false);
			SetKeyword(mat, "_OUT_FULL", true);
		}

		if(hdr == true)
		{
			SetKeyword(mat, "_TONEMAP", false);
			SetKeyword(mat, "_ADD_BLUENOISE", false);
		} else
		{
			SetKeyword(mat, "_TONEMAP", tonemap);
			SetKeyword(mat, "_ADD_BLUENOISE", blueNoise);
		}

		mat.SetFloat("_Exposure", Mathf.Pow(2.0f, exposure));
		mat.SetFloat("_ToneMap_Shoulder", tonemapShoulder);
		mat.SetFloat("_BlueNoiseIntensity", blueNoiseIntensity);
	}


	private void SaveTexture(RenderTexture texture)
	{
		string postfix = "_HemiOcta";
		if(skyboxOutput_Type == SkyBoxOutput_Type.FullOcta)
			postfix = "_FullOcta";

		string extension = "png";
		if(hdr == true)
			extension = "exr";

		string directoryName = new FileInfo(AssetDatabase.GetAssetPath(skyBoxSource)).DirectoryName;

		string filePath = EditorUtility.SaveFilePanel("Octahedron Sky", directoryName, skyBoxSource.name + postfix, extension);
		if(filePath.Length == 0)
			return;

		RenderTexture outTexture_8bit = null;
		if(hdr == false)
		{
			outTexture_8bit = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(texture, outTexture_8bit);

			RenderTexture.active = outTexture_8bit;
		} else
		{
			RenderTexture.active = texture;
		}

		Texture2D tex2d = new Texture2D(texture.width, texture.height, hdr ? DefaultFormat.HDR : DefaultFormat.LDR, TextureCreationFlags.None);
		tex2d.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
		tex2d.Apply();

		RenderTexture.active = null;
	
		if(hdr == false)
			RenderTexture.ReleaseTemporary(outTexture_8bit);

		if(hdr == true)
			File.WriteAllBytes(filePath, tex2d.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));
		else
			File.WriteAllBytes(filePath, tex2d.EncodeToPNG());

		AssetDatabase.Refresh();

		int relativeIndex = filePath.IndexOf("Assets/");
		if(relativeIndex >= 0)
		{
			filePath = filePath.Substring(relativeIndex);
			TextureImporter importer = TextureImporter.GetAtPath(filePath) as TextureImporter;
			if(importer != null)
			{
				importer.alphaSource = TextureImporterAlphaSource.None;
				importer.mipmapEnabled = false;
				importer.wrapMode = TextureWrapMode.Clamp;
				AssetDatabase.ImportAsset(filePath);
			}
		}
	}


	private void SetKeyword(Material mat, string keyword, bool state)
	{
		if(state == true)
			mat.EnableKeyword(keyword);
		else
			mat.DisableKeyword(keyword);
	}
}
