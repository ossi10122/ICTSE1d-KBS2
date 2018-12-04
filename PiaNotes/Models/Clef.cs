﻿using PiaNotes.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiaNotes.Models
{
    class Clef : IGameObject
    {
        enum ClefTypes { Treble, Bass, Alto, Tenor }

        public int PosX { get; set; }
        public int PosY { get; set; }
        private ClefTypes name;
    }
}