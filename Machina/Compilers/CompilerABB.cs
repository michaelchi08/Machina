﻿using System;
using System.Collections.Generic;

namespace Machina
{
    //   ██████╗ ██████╗ ███╗   ███╗██████╗ ██╗██╗     ███████╗██████╗ 
    //  ██╔════╝██╔═══██╗████╗ ████║██╔══██╗██║██║     ██╔════╝██╔══██╗
    //  ██║     ██║   ██║██╔████╔██║██████╔╝██║██║     █████╗  ██████╔╝
    //  ██║     ██║   ██║██║╚██╔╝██║██╔═══╝ ██║██║     ██╔══╝  ██╔══██╗
    //  ╚██████╗╚██████╔╝██║ ╚═╝ ██║██║     ██║███████╗███████╗██║  ██║
    //   ╚═════╝ ╚═════╝ ╚═╝     ╚═╝╚═╝     ╚═╝╚══════╝╚══════╝╚═╝  ╚═╝

    //   █████╗ ██████╗ ██████╗ 
    //  ██╔══██╗██╔══██╗██╔══██╗
    //  ███████║██████╔╝██████╔╝
    //  ██╔══██║██╔══██╗██╔══██╗
    //  ██║  ██║██████╔╝██████╔╝
    //  ╚═╝  ╚═╝╚═════╝ ╚═════╝                 
    /// <summary>
    /// A compiler for ABB 6-axis industrial robotic arms.
    /// </summary>
    internal class CompilerABB : Compiler
    {

        internal CompilerABB() : base("!") { }

        /// <summary>
        /// A Set of RAPID's predefined zone values. 
        /// </summary>
        private static HashSet<int> PredefinedZones = new HashSet<int>()
        {
            0, 1, 5, 10, 15, 20, 30, 40, 50, 60, 80, 100, 150, 200
        };

        /// <summary>
        /// Creates a textual program representation of a set of Actions using native RAPID Laguage.
        /// WARNING: this method is EXTREMELY UNSAFE; it performs no IK calculations, assigns default [0,0,0,0] 
        /// robot configuration and assumes the robot controller will figure out the correct one.
        /// </summary>
        /// <param name="programName"></param>
        /// <param name="writePointer"></param>
        /// <param name="block">Use actions in waiting queue or buffer?</param>
        /// <returns></returns>
        //public override List<string> UNSAFEProgramFromBuffer(string programName, RobotCursor writePointer, bool block)
        public override List<string> UNSAFEProgramFromBuffer(string programName, RobotCursor writer, bool block, bool inlineTargets, bool humanComments)
        {
            ADD_ACTION_STRING = humanComments;

            // Which pending Actions are used for this program?
            // Copy them without flushing the buffer.
            List<Action> actions = block ?
                writer.actionBuffer.GetBlockPending(false) :
                writer.actionBuffer.GetAllPending(false);


            // CODE LINES GENERATION
            // VELOCITY & ZONE DECLARATIONS
            // Amount of different VZ types
            Dictionary<int, string> velNames = new Dictionary<int, string>();
            Dictionary<int, string> velDecs = new Dictionary<int, string>();
            Dictionary<int, string> zoneNames = new Dictionary<int, string>();
            Dictionary<int, string> zoneDecs = new Dictionary<int, string>();
            Dictionary<int, bool> zonePredef = new Dictionary<int, bool>();

            // TOOL DECLARATIONS
            Dictionary<Tool, string> toolNames = new Dictionary<Tool, string>();
            Dictionary<Tool, string> toolDecs = new Dictionary<Tool, string>();

            // Intro
            List<string> introLines = new List<string>();

            // Declarations
            List<string> velocityLines = new List<string>();
            List<string> zoneLines = new List<string>();
            List<string> toolLines = new List<string>();

            // TARGETS AND INSTRUCTIONS
            List<string> variableLines = new List<string>();
            List<string> instructionLines = new List<string>();

            // DATA GENERATION
            // Use the write robot pointer to generate the data
            int it = 0;
            string line = null;
            bool usesIO = false;
            foreach (Action a in actions)
            {
                // Move writerCursor to this action state
                writer.ApplyNextAction();  // for the buffer to correctly manage them 

                // For ABB robots, check if any IO command is issued, and display a warning about configuring their names in the controller.
                if (!usesIO && (writer.lastAction.type == ActionType.IODigital || writer.lastAction.type == ActionType.IOAnalog))
                {
                    usesIO = true;
                }

                // Check velocity + zone and generate data accordingly
                if (!velNames.ContainsKey(writer.speed))
                {
                    velNames.Add(writer.speed, "vel" + writer.speed);
                    velDecs.Add(writer.speed, GetSpeedValue(writer));
                }

                if (!zoneNames.ContainsKey(writer.zone))
                {
                    bool predef = PredefinedZones.Contains(writer.zone);
                    zonePredef.Add(writer.zone, predef);
                    zoneNames.Add(writer.zone, (predef ? "z" : "zone") + writer.zone);  // use predef syntax or clean new one
                    zoneDecs.Add(writer.zone, predef ? "" : GetZoneValue(writer));
                }

                if (writer.tool != null && !toolNames.ContainsKey(writer.tool))
                {
                    toolNames.Add(writer.tool, writer.tool.name);
                    toolDecs.Add(writer.tool, GetToolValue(writer));
                }


                // Generate program
                if (inlineTargets)
                {
                    // Generate lines of code
                    if (GenerateInstructionDeclaration(a, writer, velNames, zoneNames, toolNames, out line))
                    {
                        instructionLines.Add(line);
                    }
                }
                else
                {
                    // Generate lines of code
                    if (GenerateVariableDeclaration(a, writer, it, out line))  // there will be a number jump on target-less instructions, but oh well...
                    {
                        variableLines.Add(line);
                    }

                    if (GenerateInstructionDeclarationFromVariable(a, writer, it, velNames, zoneNames, toolNames, out line))  // there will be a number jump on target-less instructions, but oh well...
                    {
                        instructionLines.Add(line);
                    }
                }

                // Move on
                it++;
            }


            // Generate V+Z+T
            foreach (Tool t in toolNames.Keys)
            {
                zoneLines.Add(string.Format("  PERS tooldata {0} := {1};", toolNames[t], toolDecs[t]));
            }
            foreach (int v in velNames.Keys)
            {
                velocityLines.Add(string.Format("  CONST speeddata {0} := {1};", velNames[v], velDecs[v]));
            }
            foreach (int z in zoneNames.Keys)
            {
                if (!zonePredef[z])  // no need to add declarations for predefined zones
                {
                    zoneLines.Add(string.Format("  CONST zonedata {0} := {1};", zoneNames[z], zoneDecs[z]));
                }
            }

            // Generate IO warning
            if (usesIO)
            {
                introLines.Add(string.Format("  {0} NOTE: your program is interfacing with the robot's IOs. Make sure to properly configure their names/properties through system preferences in the ABB controller. See Machina's `SetIOName()` for more information.",
                    commentCharacter));
            }



            // PROGRAM ASSEMBLY
            // Initialize a module list
            List<string> module = new List<string>();

            // MODULE HEADER
            module.Add("MODULE " + programName);
            module.Add("");

            // INTRO LINES
            if (introLines.Count != 0)
            {
                module.AddRange(introLines);
                module.Add("");
            }

            // VARIABLE DECLARATIONS
            // Velocities
            if (velocityLines.Count != 0)
            {
                module.AddRange(velocityLines);
                module.Add("");
            }

            // Zones
            if (zoneLines.Count != 0)
            {
                module.AddRange(zoneLines);
                module.Add("");
            }

            // Targets
            if (variableLines.Count != 0)
            {
                module.AddRange(variableLines);
                module.Add("");
            }

            // MAIN PROCEDURE
            module.Add("  PROC main()");
            module.Add(@"    ConfJ \Off;");
            module.Add(@"    ConfL \Off;");
            module.Add("");

            // Instructions
            if (instructionLines.Count != 0)
            {
                module.AddRange(instructionLines);
                module.Add("");
            }

            module.Add("  ENDPROC");
            module.Add("");

            // MODULE FOOTER
            module.Add("ENDMODULE");

            return module;
        }




        //  ╦ ╦╔╦╗╦╦  ╔═╗
        //  ║ ║ ║ ║║  ╚═╗
        //  ╚═╝ ╩ ╩╩═╝╚═╝
        internal bool GenerateVariableDeclaration(Action action, RobotCursor cursor, int id, out string declaration)
        {
            string dec = null;
            switch (action.type)
            {
                case ActionType.Translation:
                case ActionType.Rotation:
                case ActionType.Transformation:
                    dec = string.Format("  CONST robtarget target{0} := {1};", id, GetUNSAFERobTargetValue(cursor));
                    break;

                case ActionType.Axes:
                    dec = string.Format("  CONST jointtarget target{0} := {1};", id, GetJointTargetValue(cursor));
                    break;
            }

            declaration = dec;
            return dec != null;
        }

        internal bool GenerateInstructionDeclarationFromVariable(
            Action action,
            RobotCursor cursor,
            int id,
            Dictionary<int, string> velNames,
            Dictionary<int, string> zoneNames,
            Dictionary<Tool, string> toolNames,
            out string declaration)
        {
            string dec = null;
            switch (action.type)
            {
                case ActionType.Translation:
                case ActionType.Rotation:
                case ActionType.Transformation:
                    dec = string.Format("    {0} target{1}, {2}, {3}, {4}\\{5};",
                        cursor.motionType == MotionType.Joint ? "MoveJ" : "MoveL",
                        id,
                        velNames[cursor.speed],
                        zoneNames[cursor.zone],
                        cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
                        "WObj:=WObj0");
                    break;

                case ActionType.Axes:
                    dec = string.Format("    MoveAbsJ target{0}, {1}, {2}, {3}\\{4};",
                        id,
                        velNames[cursor.speed],
                        zoneNames[cursor.zone],
                        cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
                        "WObj:=WObj0");
                    break;

                case ActionType.Message:
                    ActionMessage am = (ActionMessage)action;
                    dec = string.Format("    TPWrite \"{0}\";",
                        am.message.Length <= 80 ?
                            am.message :
                            am.message.Substring(0, 80));  // ABB TPWrite messages can only be 80 chars long
                    break;

                case ActionType.Wait:
                    ActionWait aw = (ActionWait)action;
                    dec = string.Format("    WaitTime {0};",
                        0.001 * aw.millis);
                    break;

                case ActionType.Comment:
                    ActionComment ac = (ActionComment)action;
                    dec = string.Format("    {0} {1}",
                        commentCharacter,
                        ac.comment);
                    break;

                case ActionType.Attach:
                    ActionAttach aa = (ActionAttach)action;
                    dec = string.Format("    {0} Tool \"{1}\" attached",  // this action has no actual RAPID instruction, just add a comment
                        commentCharacter,
                        aa.tool.name);
                    break;

                case ActionType.Detach:
                    ActionDetach ad = (ActionDetach)action;
                    dec = string.Format("    {0} Tool detached",  // this action has no actual RAPID instruction, just add a comment
                        commentCharacter);
                    break;

                case ActionType.IODigital:
                    ActionIODigital aiod = (ActionIODigital)action;
                    if (aiod.pin < 0 || aiod.pin >= cursor.digitalOutputs.Length)
                    {
                        dec = string.Format("    {0} ERROR on \"{1}\": IO number not available",
                            commentCharacter,
                            aiod.ToString());
                    }
                    else
                    {
                        dec = string.Format("    SetDO {0}, {1};",
                        cursor.digitalOutputNames[aiod.pin],
                        aiod.on ? "1" : "0");
                    }
                    break;

                case ActionType.IOAnalog:
                    ActionIOAnalog aioa = (ActionIOAnalog)action;
                    if (aioa.pin < 0 || aioa.pin >= cursor.analogOutputs.Length)
                    {
                        dec = string.Format("    {0} ERROR on \"{1}\": IO number not available",
                            commentCharacter,
                            aioa.ToString());
                    }
                    else
                    {
                        dec = string.Format("    SetAO {0}, {1};",
                        cursor.analogOutputNames[aioa.pin],
                        aioa.value);
                    }
                    break;

                    //default:
                    //    dec = string.Format("    ! ACTION \"{0}\" NOT IMPLEMENTED", action);
                    //    break;
            }

            if (ADD_ACTION_STRING && action.type != ActionType.Comment)
            {
                dec = string.Format("{0}  {1} [{2}]",
                    dec,
                    commentCharacter,
                    action.ToString());
            }
            else if (ADD_ACTION_ID)
            {
                dec = string.Format("{0}  {1} [{2}]",
                    dec,
                    commentCharacter,
                    action.id);
            }

            declaration = dec;
            return dec != null;
        }

        internal bool GenerateInstructionDeclaration(
            Action action,
            RobotCursor cursor,
            Dictionary<int, string> velNames,
            Dictionary<int, string> zoneNames,
            Dictionary<Tool, string> toolNames,
            out string declaration)
        {
            string dec = null;
            switch (action.type)
            {
                case ActionType.Translation:
                case ActionType.Rotation:
                case ActionType.Transformation:
                    dec = string.Format("    {0} {1}, {2}, {3}, {4}\\{5};",
                        cursor.motionType == MotionType.Joint ? "MoveJ" : "MoveL",
                        GetUNSAFERobTargetValue(cursor),
                        velNames[cursor.speed],
                        zoneNames[cursor.zone],
                        cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
                        "WObj:=WObj0");
                    break;

                case ActionType.Axes:
                    dec = string.Format("    MoveAbsJ {0}, {1}, {2}, {3}\\{4};",
                        GetJointTargetValue(cursor),
                        velNames[cursor.speed],
                        zoneNames[cursor.zone],
                        cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
                        "WObj:=WObj0");
                    break;

                case ActionType.Message:
                    ActionMessage am = (ActionMessage)action;
                    dec = string.Format("    TPWrite \"{0}\";",
                        am.message.Length <= 80 ?
                            am.message :
                            am.message.Substring(0, 80));  // ABB TPWrite messages can only be 80 chars long
                    break;

                case ActionType.Wait:
                    ActionWait aw = (ActionWait)action;
                    dec = string.Format("    WaitTime {0};",
                        0.001 * aw.millis);
                    break;

                case ActionType.Comment:
                    ActionComment ac = (ActionComment)action;
                    dec = string.Format("    {0} {1}",
                        commentCharacter,
                        ac.comment);
                    break;

                case ActionType.Attach:
                    ActionAttach aa = (ActionAttach)action;
                    dec = string.Format("    {0} Tool \"{1}\" attached",  // this action has no actual RAPID instruction, just add a comment
                        commentCharacter,
                        aa.tool.name);
                    break;

                case ActionType.Detach:
                    ActionDetach ad = (ActionDetach)action;
                    dec = string.Format("    {0} Tools detached",   // this action has no actual RAPID instruction, just add a comment
                        commentCharacter);
                    break;

                case ActionType.IODigital:
                    ActionIODigital aiod = (ActionIODigital)action;
                    if (aiod.pin < 0 || aiod.pin >= cursor.digitalOutputs.Length)
                    {
                        dec = string.Format("    {0} ERROR on \"{1}\": IO number not available",
                            commentCharacter,
                            aiod.ToString());
                    }
                    else
                    {
                        dec = string.Format("    SetDO {0}, {1};",
                        cursor.digitalOutputNames[aiod.pin],
                        aiod.on ? "1" : "0");
                    }
                    break;

                case ActionType.IOAnalog:
                    ActionIOAnalog aioa = (ActionIOAnalog)action;
                    if (aioa.pin < 0 || aioa.pin >= cursor.analogOutputs.Length)
                    {
                        dec = string.Format("    {0} ERROR on \"{1}\": IO number not available",
                            commentCharacter,
                            aioa.ToString());
                    }
                    else
                    {
                        dec = string.Format("    SetAO {0}, {1};",
                        cursor.analogOutputNames[aioa.pin],
                        aioa.value);
                    }
                    break;

                    //default:
                    //    dec = string.Format("    ! ACTION \"{0}\" NOT IMPLEMENTED", action);
                    //    break;

            }

            if (ADD_ACTION_STRING && action.type != ActionType.Comment)
            {
                dec = string.Format("{0}{1}  {2} [{3}]",
                    dec,
                    dec == null ? "  " : "",  // add indentation to align with code
                    commentCharacter,
                    action.ToString());
            }
            else if (ADD_ACTION_ID)
            {
                dec = string.Format("{0}{1}  {2} [{3}]",
                    dec,
                    dec == null ? "  " : "",  // add indentation to align with code
                    commentCharacter,
                    action.id);
            }

            declaration = dec;
            return dec != null;
        }

        /// <summary>
        /// Returns an RAPID robtarget representation of the current state of the cursor.
        /// WARNING: this method is EXTREMELY UNSAFE; it performs no IK calculations, assigns default [0,0,0,0] 
        /// robot configuration and assumes the robot controller will figure out the correct one.
        /// </summary>
        /// <returns></returns>
        static internal string GetUNSAFERobTargetValue(RobotCursor cursor)
        {
            return string.Format("[{0}, {1}, {2}, {3}]",
                cursor.position.ToString(false),
                cursor.rotation.Q.ToString(false),
                "[0,0,0,0]",  // no IK at this moment
                "[0,9E9,9E9,9E9,9E9,9E9]");  // no external axes at this moment
        }

        /// <summary>
        /// Returns an RAPID jointtarget representation of the current state of the cursor.
        /// </summary>
        /// <returns></returns>
        static internal string GetJointTargetValue(RobotCursor cursor)
        {
            return string.Format("[{0}, [0,9E9,9E9,9E9,9E9,9E9]]", cursor.joints);
        }

        /// <summary>
        /// Returns a RAPID representation of cursor speed.
        /// </summary>
        /// <param name="speed"></param>
        /// <returns></returns>
        static internal string GetSpeedValue(RobotCursor cursor)
        {
            // Default speed declarations in ABB always use 500 deg/s as rot speed, but it feels too fast (and scary). 
            // Using the same value as lin motion here.
            return string.Format("[{0},{1},{2},{3}]", cursor.speed, cursor.speed, 5000, 1000);
        }

        /// <summary>
        /// Returns a RAPID representatiton of cursor zone.
        /// </summary>
        /// <param name="cursor"></param>
        /// <returns></returns>
        static internal string GetZoneValue(RobotCursor cursor)
        {
            // Following conventions for default RAPID zones.
            double high = 1.5 * cursor.zone;
            double low = 0.15 * cursor.zone;
            return string.Format("[FALSE,{0},{1},{2},{3},{4},{5}]", cursor.zone, high, high, low, high, low);
        }

        /// <summary>
        /// Returns a RAPID representation of a Tool object.
        /// </summary>
        /// <param name="cursor"></param>
        /// <returns></returns>
        static internal string GetToolValue(RobotCursor cursor)  //TODO: wouldn't it be just better to pass the Tool object? Inconsistent with the rest of the API...
        {
            if (cursor.tool == null)
            {
                throw new Exception("Cursor has no tool attached");
            }

            return string.Format("[TRUE, [{0},{1}], [{2},{3},{4},0,0,0]]",
                cursor.tool.TCPPosition,
                cursor.tool.TCPOrientation.Q.ToString(false),
                cursor.tool.weight,
                cursor.tool.centerOfGravity,
                "[1,0,0,0]");  // no internial axes by default
        }

    }
}
