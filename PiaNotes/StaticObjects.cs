﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace PiaNotes
{
    static class StaticObjects
    {

        /// <summary>
        /// Dialogs
        /// </summary>

        //Popup dialog for if you have no midi devices but still want to run the practisepage
        public static ContentDialog NoMidiInOutDialog = new ContentDialog
        {
            Title = "There is no MIDI in- or output!",
            Content = "Check your MIDI input and output device before practicing.",
            CloseButtonText = "Ok"
        };

        //Popup Dialog in case of no connection with the Database
        public static ContentDialog NoDatabaseConnectionDialog = new ContentDialog
        {
            Title = "There is no Internet connection!",
            Content = "Add a local MIDI file before practicing.",
            CloseButtonText = "Ok"
        };

        //Popup Dialog if MIDI octaves are out of range.
        public static ContentDialog MidiOutOfRange = new ContentDialog
        {
            Title = "MIDI file is out of range!",
            Content = "The selected MIDI file has octaves too far from each other to be loaded properly. Please select a different file.",
            CloseButtonText = "Ok"
        };


    }
}
