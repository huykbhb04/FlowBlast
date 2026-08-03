using FlowBlast.Gameplay.Grid;
using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    public sealed class BoxVisualView : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [SerializeField] private Renderer _renderer;
        [SerializeField] private BoxVisualPaletteSO _palette;

        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        public void SetPalette(BoxVisualPaletteSO palette)
        {
            _palette = palette;
        }

        public void Apply(BoxColor color)
        {
            if (_renderer == null || _palette == null)
            {
                return;
            }

            if (_palette.SharedMaterial != null)
            {
                _renderer.sharedMaterial = _palette.SharedMaterial;
            }

            if (!_palette.TryGetTexture(color, out Texture texture))
            {
                return;
            }

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(BaseMapId, texture);
            _propertyBlock.SetTexture(MainTexId, texture);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
