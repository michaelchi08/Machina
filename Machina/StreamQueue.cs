﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Machina
{
    /// <summary>
    /// A queue manager for 'stream' mode and Frame objects
    /// </summary>
    internal class StreamQueue
    {
        // DEACTIVATED EVERYTHING BECAUSE FRAME OBJECTS ARE NOT UPDATED, AND ARE ONLY USED HERE. 
        // TO REVIEW WHEN GOING BACK TO STREAMING MODE. 

        //private List<Frame> sentTargets;
        //private List<Frame> queuedTargets;

        //public StreamQueue()
        //{
        //    this.sentTargets = new List<Frame>();
        //    this.queuedTargets = new List<Frame>();
        //}

        //public void Add(Frame f)
        //{
        //    this.queuedTargets.Add(f);
        //}

        //public void Add(double x, double y, double z, double vel, double zon)
        //{
        //    Add( new Frame(x, y, z, vel, zon) );
        //}

        //public Frame GetNext()
        //{
        //    if (queuedTargets.Count == 0) return null;

        //    sentTargets.Add(queuedTargets[0]);
        //    queuedTargets.RemoveAt(0);

        //    return sentTargets.Last();
        //}

        //public bool AreFramesPending()
        //{
        //    return queuedTargets.Count > 0;
        //}

        //public int FramesPending()
        //{
        //    return queuedTargets.Count();
        //}

        //public bool EmptyQueue()
        //{
        //    bool rem = AreFramesPending();
        //    queuedTargets.Clear();
        //    return rem;
        //}
        

    }

}
