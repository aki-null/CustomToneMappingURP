using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    internal readonly struct CustomLutConfig
    {
        internal readonly Texture2D Texture;
        private readonly int _instanceId;
        private readonly int _width;
        private readonly int _height;
        internal readonly Vector3 SampleParams;

        private CustomLutConfig(Texture2D texture)
        {
            Texture = texture;
            _instanceId = texture == null ? 0 : texture.GetInstanceID();
            _width = texture == null ? 0 : texture.width;
            _height = texture == null ? 0 : texture.height;
            SampleParams = texture == null
                ? default
                : new Vector3(1.0f / texture.width, 1.0f / texture.height, texture.height - 1);
        }

        internal static bool TryCreate(Texture2D texture, out CustomLutConfig config, out string error)
        {
            config = new CustomLutConfig(texture);
            return config.TryValidate(out error);
        }

        internal static bool TryValidate(Texture2D texture, out string error)
        {
            return TryCreate(texture, out _, out error);
        }

        internal bool Matches(Texture2D texture)
        {
            // Reference identity is intentional. Unity's overloaded == treats
            // destroyed objects as null, which can incorrectly merge distinct
            // destroyed wrappers and share failure suppression between them.
            if (!ReferenceEquals(Texture, texture))
                return false;

            if (texture == null)
                return _instanceId == 0 && _width == 0 && _height == 0;

            return _instanceId == texture.GetInstanceID() &&
                   _width == texture.width &&
                   _height == texture.height;
        }

        private bool TryValidate(out string error)
        {
            if (Texture == null)
            {
                error = "Assign a 2D LUT texture.";
                return false;
            }

            if (Texture.dimension != TextureDimension.Tex2D)
            {
                error = "Custom LUT must be a 2D texture.";
                return false;
            }

            if (_height < 2 || _width != _height * _height)
            {
                error = "Custom LUT must use a square-strip layout: width = height × height.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
