// Inspector choices for campus colors, ground details, planting and other decoration.

using System;
using UnityEngine;

namespace Escape4Now.Map
{
    public enum CampusSurfacePattern { Natural, Stripes, Stone, Solid }
    [Serializable]
    public sealed class CampusSurfaceStyle
    {
        public OutdoorSurface surface;
        public Color primary = Color.gray, secondary = Color.gray;
        public CampusSurfacePattern pattern;
        public Material material;
    }

    [CreateAssetMenu(menuName = "Escape 4 Now/Survival/Campus Visual Settings")]
    public sealed class SurvivalCampusVisualSettings : ScriptableObject
    {
        [Header("Surface Palettes and Optional Materials")]
        public CampusSurfaceStyle[] surfaces = new CampusSurfaceStyle[0];
        [Range(0f, .15f)] public float naturalVariation = .035f;
        [Min(.01f)] public float noiseScale = .24f;
        [Min(1)] public int mowingStripeWidth = 2;
        public Color mainPath = new Color(.69f, .67f, .59f);
        public Color gardenGrass = new Color(.22f, .43f, .27f);
        [Header("Pavement and Area Details")]
        public bool showGroundDetails = true;
        [Range(0f, 1f)] public float detailDensity = .65f;
        [Min(.005f)] public float pavingJointWidth = .012f;
        [Min(.005f)] public float pathBorderWidth = .08f;
        [Min(.005f)] public float poolCopingWidth = .17f;
        public Color pathBorder = new Color(.4f, .43f, .37f);
        public Color pavingJoint = new Color(.33f, .35f, .32f, .4f);
        public Color poolCoping = new Color(.91f, .88f, .72f);
        public Color drainage = new Color(.23f, .32f, .34f);
        public Color courtyardAccent = new Color(.64f, .42f, .27f);
        public Color gardenSoil = new Color(.3f, .25f, .16f);
        public bool showGardenTrails = true;
        public Color gardenTrail = new Color(.64f, .6f, .43f);
        [Min(.02f)] public float gardenTrailWidth = .24f;
        public Color serviceWarning = new Color(.94f, .67f, .16f);
        public Color parkingCurbs = new Color(.82f, .8f, .69f);
        public Color trackStart = new Color(.98f, .95f, .85f);
        public bool smoothTrackSurface = true;
        [Min(.05f)] public float drainSpacing = .5f;
        public Vector2 drainSize = new Vector2(.15f, .035f);
        [Range(.05f, .45f)] public float courtyardPatternRadius = .25f;
        [Min(1)] public int finishMarkColumns = 6;
        [Min(.01f)] public float finishMarkSize = .16f;
        public Vector2 serviceStripeSize = new Vector2(.45f, .1f);
        [Header("Vegetation Beds and Existing Prop Footprints")]
        [Range(8, 48)] public int curveSegments = 20;
        [Min(.05f)] public float plantingBedRadius = .58f;
        [Min(.01f)] public float propShadowRadius = .48f;
        public Color propShadow = new Color(.06f, .13f, .1f, .18f);
        public SurvivalPropVisual flowerDetailPrefab;
        public Vector2Int flowersPerPlantingBed = new Vector2Int(2, 4);
        [Range(.05f, .5f)] public float flowerDetailScale = .24f;
        [Header("Rain Surface Treatment")]
        [Range(0f, 1f)] public float wetPavementOpacity = .16f;
        public Color wetPavementColor = new Color(.16f, .31f, .38f);
        public Color puddleGlint = new Color(.72f, .88f, .92f, .24f);
        [Range(0f, 1f)] public float puddleChance = .04f;
        [Min(.01f)] public float puddleRadius = .23f;
    }
}
