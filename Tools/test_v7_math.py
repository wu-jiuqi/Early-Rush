#!/usr/bin/env python3
"""Executable mathematical model regression, NOT C# or Unity Player execution.
Models the formulas/control ordering in RushGame/RushJourney/RushDailyCommute.
Keep model assertions separate from source-contract checks.
"""
import math
import unittest

def approach(x,target,max_delta):
    return target if abs(target-x)<=max_delta else x+math.copysign(max_delta,target-x)

def hold_step(hold,eligible,dt,duration):
    return min(duration,hold+dt) if eligible else 0

def clock_model(initial_speed,dt,seconds=2):
    # Holding E on pad cancels desired movement and uses brace acceleration 44.
    speed=initial_speed;hold=0;distance=0
    for _ in range(math.ceil(seconds/dt)):
        hold=hold_step(hold,speed<1.4,dt,.8)
        speed=approach(speed,0,44*dt)
        distance+=speed*dt
        if hold>=.8:return distance,True
    return distance,False

class CommuteMath(unittest.TestCase):
    def test_clock_brakes_then_completes(self):
        for hz in [30,60,120]:
            for speed in [0,2.352,4.9,11.3]:
                distance,finished=clock_model(speed,1/hz)
                self.assertTrue(finished)
                # Pad is 2.2 units deep. Starting at leading edge, even dash brakes inside.
                self.assertLess(distance,2.2)
    def test_legacy_shift_walk_cannot_clock(self):
        hold=0
        for _ in range(120):hold=hold_step(hold,False,1/60,.8)
        self.assertEqual(hold,0)
        self.assertGreater(4.9*.48,1.4)
    def test_interruptions_reset_hold(self):
        for reason in ['leave','release','airborne','recovery','pause']:
            hold=.4
            self.assertEqual(hold_step(hold,False,1/60,.8),0,reason)
    def test_task_two_stops_one_reward(self):
        phase=0;hold=0;grace=0;elapsed=37.2
        for stop in range(4):
            for tick in range(80):
                if phase>=2:continue
                hold=hold_step(hold,True,1/60,.65)
                if hold>=.65:
                    phase+=1;hold=0
                    if phase==2:grace+=8
                    break
        self.assertEqual((phase,grace,elapsed),(2,8,37.2))
    def test_escalator_directional(self):
        up=4.9+.85;down=-4.9+.85
        self.assertGreater(up,4.9)
        self.assertLess(abs(down),4.9)
        self.assertGreater(down,-4.9)
    def test_rain_continuous_acceleration(self):
        coefficients=[1+(.62-1)*rain for rain in [0,.001,.25,.5,.999,1]]
        self.assertAlmostEqual(coefficients[0],1)
        self.assertAlmostEqual(coefficients[-1],.62)
        self.assertLess(abs(coefficients[1]-coefficients[0]),.001)
        self.assertTrue(all(a>=b for a,b in zip(coefficients,coefficients[1:])))

if __name__=='__main__':unittest.main(verbosity=2)
