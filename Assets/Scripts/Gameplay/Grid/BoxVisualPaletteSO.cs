using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlowBlast.Gameplay.Grid
{
    [CreateAssetMenu(fileName = "BoxVisualPalette", menuName = "FlowBlast/Box Visual Palette", order = 2)]
    public sealed class BoxVisualPaletteSO : ScriptableObject
    {
        [SerializeField] private Material _sharedMaterial;
        [SerializeField] private List<BoxTextureEntry> _textures = new List<BoxTextureEntry>();

        public Material SharedMaterial => _sharedMaterial;

        private void OnValidate()
        {
            EnsureEntries();
        }

        private void Reset()
        {
            EnsureEntries();
        }

        private void EnsureEntries()
        {
            BoxColor[] colors = (BoxColor[])Enum.GetValues(typeof(BoxColor));
            for (int i = 0; i < colors.Length; i++)
            {
                if (!ContainsColor(colors[i]))
                {
                    _textures.Add(new BoxTextureEntry(colors[i], null));
                }
            }
        }

        private bool ContainsColor(BoxColor color)
        {
            for (int i = 0; i < _textures.Count; i++)
            {
                if (_textures[i].Color == color)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetTexture(BoxColor color, out Texture texture)
        {
            for (int i = 0; i < _textures.Count; i++)
            {
                BoxTextureEntry entry = _textures[i];
                if (entry.Color == color)
                {
                    texture = entry.Texture;
                    return texture != null;
                }
            }

            texture = null;
            return false;
        }
    }

    [Serializable]
    public struct BoxTextureEntry
    {
        public BoxColor Color;
        public Texture2D Texture;

        public BoxTextureEntry(BoxColor color, Texture2D texture)
        {
            Color = color;
            Texture = texture;
        }
    }
}
