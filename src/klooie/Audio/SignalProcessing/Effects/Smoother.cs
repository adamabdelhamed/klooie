using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
static class Smoother
{
    // single-pole envelope follower
    public static float Follow(ref float state, float coeffRise, float coeffFall, float x)
    {
        state += (x > state ? coeffRise : coeffFall) * (x - state);
        return state;
    }
}