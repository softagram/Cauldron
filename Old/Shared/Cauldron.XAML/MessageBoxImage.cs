﻿namespace Cauldron.XAML
{
    /// <summary>
    /// Specifies the icon that is displayed by a message box.
    /// </summary>
    public enum MessageBoxImage : int
    {
        /// <summary>
        /// No icon is displayed.
        /// </summary>
        None = 0,

        /// <summary>
        /// The message box contains a symbol consisting of white X in a circle with a red background.
        /// </summary>
        Error = 16,

        /// <summary>
        /// The message box contains a symbol consisting of a question mark in a circle.
        /// </summary>
        Question = 32,

        /// <summary>
        /// The message box contains a symbol consisting of an exclamation point in a triangle with a yellow background.
        /// </summary>
        Exclamation = 48,

        /// <summary>
        /// The message box contains a symbol consisting of a lowercase letter i in a circle.
        /// </summary>
        Information = 64
    }
}