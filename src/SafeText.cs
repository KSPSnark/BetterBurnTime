using System;
using UnityEngine.UI;

namespace BetterBurnTime
{
    /// <summary>
    /// Provides a safe wrapper to avoid getting NullReferenceExceptions from Unity 5. See comments
    /// on their bizarre, insane implementation of operator== here:
    /// http://blogs.unity3d.com/2014/05/16/custom-operator-should-we-keep-it/
    /// ...the moral of the story being, if you have a persistent reference to a UI object,
    /// you have to check it for "== null" every time you want to use it.
    /// </summary>
    class SafeText
    {
        private Text text;

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
            // use ReferenceEquals here because Unity does weird overloading of operator ==
            if (Object.ReferenceEquals(text, null)) throw new ArgumentNullException("text cannot be null");
            return new SafeText(text);
        }

        public void Destroy()
        {
            if (text != null)
            {
                UnityEngine.Object.Destroy(text);
                text = null;
            }
        }

        /// <summary>
        /// Gets or sets the text value.
        /// </summary>
        public string Text
        {
            get { return IsNullText ? string.Empty : text.text; }
            set { if (!IsNullText) text.text = value; }
        }

        /// <summary>
        /// Gets or sets whether the text is currently enabled.
        /// </summary>
        public bool Enabled
        {
            get { return IsNullText ? false : text.enabled; }
            set { if (!IsNullText) text.enabled = value; }
        }

        /// <summary>
        /// Gets/sets whether the text is currently in an error state and shouldn't be touched.
        /// </summary>
        private bool IsNullText
        {
            get
            {
                // Note, we have to check this even though text is a readonly variable that we
                // established as non-null at construction time, due to stupid Unity design.
                // We're calling the bizarrely-overloaded operator == here, not an actual == null.
                return text == null;
            }
        }
    }
}
