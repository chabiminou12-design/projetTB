// Helpers/AvatarHelper.cs
namespace Stat.Helpers
{
    public static class AvatarHelper
    {
        public static string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "?";

            var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                return $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpper();
            }
            return $"{parts[0][0]}".ToUpper();
        }

        public static string GetBackgroundColor(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "#808080"; // Gray for unknown

            var hash = fullName.GetHashCode();
            var r = (hash & 0xFF0000) >> 16;
            var g = (hash & 0x00FF00) >> 8;
            var b = hash & 0x0000FF;
            return $"rgb({r}, {g}, {b})";
        }
    }
}