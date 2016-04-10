using System;
using UnityEngine.UI;

namespace BetterBurnTime
{
    /// <summary>
    /// The existence of this class is basically a kludge. With the update to Unity5 in KSP 1.1,
    /// this mod has started running into NullReferenceException a lot when try to check
    /// the "enabled" property on a UnityEngine.UI.Text object. That is, I have a non-null
    /// Text object, and when I try to access the "enabled" property on it, it sometimes
    /// throws a NullReferenceException.
    ///
    /// I have no idea why it does this, or what the conditions are that cause it to do so
    /// occasionally. I don't know whether it's a bug in Unity, or whether I'm somehow using
    /// it wrong (though what the requirements are, I have no idea-- I'm using it in the exact
    /// same way I used to use ScreenSafeGUIText in the old Unity4).
    ///
    /// The problem is that when the bug kicks in, it throws an exception every time, and my
    /// mod needs to ask "are you enabled" a lot (on every frame), and something about spamming
    /// that seems to leak memory rapidly. Therefore, I'm writing this SafeText kludge as a
    /// bulletproof wrapper around the Text. It traps any exceptions that get thrown, and if/when
    /// an exception *does* happen, it starts a cooldown timer that prevents actually accessing the
    /// Text object for the next second. The hope is that this will protect the mod (and thus
    /// the player) from the toxic effects of spamming the exceptions.
    /// </summary>
    class SafeText
    {
        private static readonly TimeSpan COOLDOWN = new TimeSpan(0, 0, 1); // one second

        private readonly Text text;
        private DateTime errorTimestamp = DateTime.MinValue;

        private SafeText(Text text)
        {
            this.text = text;
        }

        /// <summary>
        /// Get a new SafeText that wraps the specified text object.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static SafeText of(Text text)
        {
            if (text == null) throw new ArgumentNullException("text cannot be null");
            return new SafeText(text);
        }

        /// <summary>
        /// Gets or sets the text value.
        /// </summary>
        public string Text
        {
            get
            {
                if (IsError) return string.Empty;
                try
                {
                    return text.text;
                }
                catch (Exception e)
                {
                    Logging.Exception("Error trying to get text value", e);
                    IsError = true;
                    return string.Empty;
                }
            }
            set
            {
                if (IsError) return;
                try
                {
                    text.text = value;
                }
                catch (Exception e)
                {
                    Logging.Exception("Error trying to set text value", e);
                    IsError = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the text is currently enabled.
        /// </summary>
        public bool Enabled
        {
            get
            {
                if (IsError) return false;
                try
                {
                    return text.enabled;
                }
                catch (Exception e)
                {
                    Logging.Exception("Error trying to get whether text is enabled", e);
                    IsError = true;
                    return false;
                }
            }
            set
            {
                if (IsError) return;
                try
                {
                    text.enabled = value;
                }
                catch (Exception e)
                {
                    Logging.Exception("Error trying to set whether text is enabled", e);
                    IsError = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Gets/sets whether the text is currently in an error state and shouldn't be touched.
        /// </summary>
        private bool IsError
        {
            get
            {
                if (errorTimestamp == DateTime.MinValue)
                {
                    return false;
                }
                else
                {
                    bool hasRecentError = DateTime.Now < (errorTimestamp + COOLDOWN);
                    if (!hasRecentError) errorTimestamp = DateTime.MinValue;
                    return hasRecentError;
                }
            }
            set
            {
                errorTimestamp = value ? DateTime.Now : DateTime.MinValue;
            }
        }
    }
}
