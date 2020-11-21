using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace msfsLegacyImporter
{
    class fsxVarHelper
    {
        public string fsx2msfsSimVar(string fsxSimVar, xmlHelper XmlHelper, bool returnVariable = true)
        {
            fsxSimVar = Regex.Replace(fsxSimVar, "\r\n|\r|\n", "");
            fsxSimVar = Regex.Replace(fsxSimVar, @"\s\s+", " ");
            fsxSimVar = fsxSimVar.Replace(")!", ") !").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&amp;", "&");

            string fsxVariable = fsxSimVar;
            List<string[]> variables = new List<string[]>();

            // REPLACE VARIABLES
            Random r = new Random();
            var regex = new Regex(@"\((.*?)\)");
            foreach (var match in regex.Matches(fsxVariable))
            {
                if (match.ToString().Trim().StartsWith("(>") || match.ToString().Trim().StartsWith("( >")) // SET VARIABLE GAP
                {
                    //variables.Add(new string[] { match.ToString(), "SETVARPLACEHOLDER" });
                    fsxVariable = fsxVariable.Replace(match.ToString(), "SETVARPLACEHOLDER");
                }
                else
                {
                    string placeholder = r.Next(1000000, 9000000).ToString();
                    variables.Add(new string[] { match.ToString(), placeholder });
                    fsxVariable = fsxVariable.Replace(match.ToString(), placeholder);
                }
            }

            // PARSE FSX FORMULA
            string infix = PostfixToInfix(fsxVariable, XmlHelper);

            // INSERT VARIABLES
            if (!String.IsNullOrEmpty(infix))
            {
                infix = infix.Replace("SETVARPLACEHOLDER", "1");

                foreach (string[] variable in variables)
                {
                    string msfsVariable = getMsfsVariable(variable[0], XmlHelper);
                    infix = infix.Replace(variable[1], msfsVariable);
                }

                // REMOVE TRALING SYMBOLS
                infix = infix.Trim();
                if (infix[infix.Length - 1] == '+' || infix[infix.Length - 1] == '*' || infix[infix.Length - 1] == '/' || infix[infix.Length - 1] == '-')
                    infix = infix.Substring(infix.Length - 1).Trim();

                XmlHelper.writeLog("Expression: " + fsxSimVar);
                XmlHelper.writeLog("Parsing result: " + infix + Environment.NewLine);

                if (!String.IsNullOrEmpty(infix))
                {
                    if (returnVariable)
                        return "var ExpressionResult = " + infix + "; /* PARSED FROM \"" + fsxSimVar + "\" */";
                    else
                        return infix;
                }
            }

            XmlHelper.writeLog("NOT PARSED " + fsxSimVar + Environment.NewLine);
            if (returnVariable)
                return "var ExpressionResult = 0; /* SIM VAR \"" + fsxSimVar + "\" NOT PARSED! */";
            else
                return "0";
        }

        public string fsx2msfsGaugeString(string gaugeTextString, xmlHelper XmlHelper)
        {
            string gaugeString = Regex.Replace(gaugeTextString, "\r\n|\r|\n", "");
            gaugeString = gaugeString./*Replace(") %", ")%").Replace("% (", "%(").*/Replace(")%(", ")%%(").Trim();

            XmlHelper.writeLog("### Gauge String: " + gaugeString);
            List<string[]> gaugeScripts = new List<string[]>();

            // EXTRACT SCRIPTS
            int counter = 0;
            Random r = new Random();
            var regex = new Regex(@"((\%\s*\(.+?\)\s*\%)(![0-9a-z\.\-\+ ]+?!)?[\%]{0,1}.*?)?");
            foreach (Match script in regex.Matches(gaugeString))
            {
                if (string.IsNullOrEmpty(script.Value))
                    continue;

                XmlHelper.writeLog("Group: " + script.Value);

                string[] gaugeData = new string[4];
                foreach (var group in script.Groups) {
                    int num = counter % 4;
                    XmlHelper.writeLog("Script part #" + num + ": " + group.ToString());

                    gaugeData[num] = group.ToString();

                    if (num == 1)
                    {
                        if (!string.IsNullOrEmpty(group.ToString()))
                        {
                            string placeholder = "scriptPlaceholder" + r.Next(100000, 900000).ToString();
                            gaugeString = gaugeString.Replace(group.ToString(), placeholder);
                            gaugeData[0] = placeholder;
                        } else
                        {
                            gaugeData[0] = "";
                        }
                    }
                    else if (num == 3 && !string.IsNullOrEmpty(gaugeData[0]))
                    {
                        gaugeScripts.Add(gaugeData);
                    }

                    counter++;
                }
            }

            // ECAPE PLAIN TEXT
            string newGaugeString = "";
            if (gaugeString.Contains("scriptPlaceholder"))
            {

                var regex1 = new Regex(@"scriptPlaceholder[0-9]{6}");
                var placeholdersList = regex1.Matches(gaugeString);

                int i = 0;
                foreach (string line in Regex.Split(gaugeString, @"scriptPlaceholder[0-9]{6}"))
                {
                    if (!String.IsNullOrEmpty(line))
                    {
                        XmlHelper.writeLog("Line: " + line + "; Current: " + newGaugeString);
                        //gaugeString = gaugeString.Replace(line, "\"" + line + "\" + ");
                        newGaugeString += "\"" + line + "\" + ";
                    }

                    if (i < placeholdersList.Count)
                        newGaugeString += placeholdersList[i].ToString();

                    i++;
                }
            }

            if (newGaugeString == "")
                newGaugeString = "\"" + gaugeString + "\"";

            // REPLACE WEIRD VALUES LIKE %R%
            newGaugeString = Regex.Replace(newGaugeString, @"\%([A-Za-z])\%", "$1");

            // INSERT EXPRESSIONS
            foreach (string[] gaugeScript in gaugeScripts)
            {
                string placeholder = gaugeScript[0];
                string msfsVariable = gaugeScript[2];
                string formattingValue = gaugeScript[3].Trim('!').Trim();
                XmlHelper.writeLog("placeholder: " + placeholder + "; msfsVariable: " + msfsVariable + "; formattingValue: " + formattingValue);

                if (!string.IsNullOrEmpty(msfsVariable))
                {
                    msfsVariable = "( " + fsx2msfsSimVar(Regex.Replace(msfsVariable, @"\%\s?\(?(.*)\)\s?\%", "$1").Trim(), XmlHelper, false) + " )";

                    if (!String.IsNullOrEmpty(formattingValue))
                    {
                        if (formattingValue.Contains("s")) // STRING
                        {

                        }
                        else if (formattingValue.Contains("d"))
                        { // DIGIT
                            if (formattingValue.Contains("-"))
                                msfsVariable = "Math.min(0, Math.round" + msfsVariable + ")";
                            else if (formattingValue.Contains("+"))
                                msfsVariable = "Math.max(0, Math.round" + msfsVariable + ")";
                            else
                                msfsVariable = "Math.round" + msfsVariable + "";
                        }
                        else if (formattingValue.Contains("f")) // FLOAT
                        {
                            formattingValue = formattingValue.Replace("f", "");
                            if (formattingValue.Contains("."))
                                msfsVariable = msfsVariable + ".toFixed(" + (formattingValue.Split('.')[1]) + ")";
                        }
                    }

                    //Console.WriteLine("Replacing " + gaugeScript[0] + " in " + newGaugeString);
                    newGaugeString = newGaugeString.Replace(gaugeScript[0], msfsVariable + ".toString() + ");
                }
                else if (!string.IsNullOrEmpty(placeholder))
                {
                    newGaugeString = newGaugeString.Replace(placeholder, "");
                }
            }

            newGaugeString = newGaugeString.Trim().Trim('+');
            XmlHelper.writeLog("### Result: " + newGaugeString + Environment.NewLine);

            // %((A:Kohlsman setting hg,millibars))%!6.2f! mb
            // %\((.*?)\)%\!.*\!|%\((.*?)\)%

            return newGaugeString;
        }

        private string getMsfsVariable(string fsxVariable, xmlHelper XmlHelper)
        {
            string fsxVar = fsxVariable.Trim().Replace("( ", "(").Replace(" )", ")").Replace(" ,", ",").Replace(", ", ",").Replace("{ ", "{").
                Replace(" {", "{").Replace("} ", "}").Replace(" }", "}").Replace(": ", ":").Replace(" :", ":").Replace(", ", ",").Replace(" ,", ",");
            switch (fsxVar)
            {
                case string hk when hk.Equals("(P:Units of measure,enum)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";

                // 0 = ok, 1 = fail, 2 = blank.
                case string ha when ha.Equals("(A:PARTIAL PANEL ADF,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hb when hb.Equals("(A:PARTIAL PANEL AIRSPEED,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hc when hc.Equals("(A:PARTIAL PANEL ALTIMETER,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hd when hd.Equals("(A:PARTIAL PANEL ATTITUDE,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string he when he.Equals("(A:PARTIAL PANEL COMM,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hf when hf.Equals("(A:PARTIAL PANEL COMPASS,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hg when hg.Equals("(A:PARTIAL PANEL ELECTRICAL,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hh when hh.Equals("(A:PARTIAL PANEL AVIONICS,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hi when hi.Equals("(A:PARTIAL PANEL ENGINE,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hj when hj.Equals("(A:PARTIAL PANEL FUEL INDICATOR,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:PARTIAL PANEL HEADING,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:PARTIAL PANEL VERTICAL VELOCITY,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hm when hm.Equals("(A:PARTIAL PANEL TRANSPONDER,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hn when hn.Equals("(A:PARTIAL PANEL NAV,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:PARTIAL PANEL PITOT,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:PARTIAL PANEL TURN COORDINATOR,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string hq when hq.Equals("(A:PARTIAL PANEL VACUUM,enum)", StringComparison.InvariantCultureIgnoreCase):
                case string fj when fj.Equals("(L:HUD Power,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string fk when fk.Equals("(L:VectorOnGlass,enum)", StringComparison.InvariantCultureIgnoreCase):
                    return "0";

                case string fi when fi.Equals("(A:Avionics master switch,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string hn when hn.Equals("(A:CIRCUIT AVIONICS ON,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:PARTIAL PANEL AVIONICS,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:PARTIAL PANEL COMPASS,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string hq when hq.Equals("(A:PARTIAL PANEL ELECTRICAL,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string hr when hr.Equals("(A:PARTIAL PANEL HEADING,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string hs when hs.Equals("(A:PARTIAL PANEL NAV:1,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string ht when ht.Equals("(A:PARTIAL PANEL NAV:2,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string hu when hu.Equals("(A:Circuit general panel on,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";

                // HORIZONTAL
                case string hn when hn.Equals("(A:Airspeed select indicated or true,knots)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Airspeed select indicated or true:1,knots)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Airspeed select indicated or true:2,knots)", StringComparison.InvariantCultureIgnoreCase):
                case string hq when hq.Equals("(A:Airspeed true,knots)", StringComparison.InvariantCultureIgnoreCase):
                case string hr when hr.Equals("(A:AIRSPEED BARBER POLE,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED TRUE\", \"knots\"))";
                case string hp when hp.Equals("(A:Airspeed select indicated or true,k/h)", StringComparison.InvariantCultureIgnoreCase):
                case string hq when hq.Equals("(A:Airspeed select indicated or true,kilometers per hour)", StringComparison.InvariantCultureIgnoreCase):
                case string hr when hr.Equals("(A:Airspeed true,k/h)", StringComparison.InvariantCultureIgnoreCase):
                case string hs when hs.Equals("(A:Airspeed true,kilometers per hour)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED TRUE\", \"mph\"))";
                case string hp when hp.Equals("(A:Airspeed select indicated or true,mph)", StringComparison.InvariantCultureIgnoreCase):
                case string hq when hq.Equals("(A:Airspeed select indicated or true,miles per hour)", StringComparison.InvariantCultureIgnoreCase):
                case string hr when hr.Equals("(A:Airspeed true,mph)", StringComparison.InvariantCultureIgnoreCase):
                case string hs when hs.Equals("(A:Airspeed true,miles per hour)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED TRUE\", \"mph\"))";
                case string hp when hp.Equals("(A:Airspeed indicated,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED INDICATED\", \"knots\"))";
                case string hn when hn.Equals("(A:AIRSPEED INDICATED,mph)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED INDICATED\", \"mph\"))";
                case string hn when hn.Equals("(A:AIRSPEED INDICATED,k/h)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:AIRSPEED INDICATED,kilometers per hour)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED INDICATED\", \"kilometers per hour\"))";
                case string hn when hn.Equals("(A:Airspeed mach,machs)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Airspeed mach,mach)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED TRUE\", \"mach\"))";
                case string hn when hn.Equals("(A:ACCELERATION BODY X,feet per second squared)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ACCELERATION WORLD X\", \"feet per second squared\"), 0)";
                case string hn when hn.Equals("(A:ACCELERATION BODY Y,feet per second squared)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ACCELERATION WORLD Y\", \"feet per second squared\"), 0)";
                case string hn when hn.Equals("(A:ACCELERATION BODY Z,feet per second squared)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ACCELERATION WORLD Z\", \"feet per second squared\"), 0)";
                case string hn when hn.Equals("(A:ACCELERATION BODY X,feet per minute squared)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ACCELERATION WORLD X\", \"feet per minute squared\"), 0)";
                case string hn when hn.Equals("(A:ACCELERATION BODY Y,feet per minute squared)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ACCELERATION WORLD Y\", \"feet per minute squared\"), 0)";
                case string hn when hn.Equals("(A:ACCELERATION BODY Z,feet per minute squared)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ACCELERATION WORLD Z\", \"feet per minute squared\"), 0)";
                case string hn when hn.Equals("(A:VELOCITY WORLD X,m/s)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VELOCITY WORLD X\", \"meter per second\"))";
                case string hn when hn.Equals("(A:VELOCITY WORLD X,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VELOCITY WORLD X\", \"knots\"))";
                case string hn when hn.Equals("(A:VELOCITY WORLD Y,m/s)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VELOCITY WORLD Y\", \"meter per second\"))";
                case string hn when hn.Equals("(A:VELOCITY WORLD Y,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VELOCITY WORLD Y\", \"knots\"))";
                case string hn when hn.Equals("(A:VELOCITY WORLD Z,m/s)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:VELOCITY WORLD Z,meters per second)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VELOCITY WORLD Z\", \"meter per second\"))";
                case string hn when hn.Equals("(A:VELOCITY WORLD Z,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VELOCITY WORLD Z\", \"knots\"))";
                case string hn when hn.Equals("(A:GROUND VELOCITY,m/s)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:GROUND VELOCITY,meters per second)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:GPS Ground Speed,meters per second)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GPS GROUND SPEED\", \"meter per second\"))";
                case string hn when hn.Equals("(A:GROUND VELOCITY,knots)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:GPS Ground Speed,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GPS GROUND SPEED\", \"knots\"))";

                // VERTICAL
                case string hn when hn.Equals("(A:Vertical speed,feet per minute)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VERTICAL SPEED\", \"feet per minute\"))";
                case string hn when hn.Equals("(A:Vertical speed,meters per minute)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VERTICAL SPEED\", \"meters per minute\"))";
                case string hn when hn.Equals("(A:Vertical speed,meters per second)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VERTICAL SPEED\", \"meters per second\"))";
                case string hn when hn.Equals("(A:Indicated Altitude,meters)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Indicated Altitude:1,meters)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Indicated Altitude:2,meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE\", \"meters\"))";
                case string hp when hp.Equals("(A:PLANE ALTITUDE,nmiles)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE\", \"nautical miles\"))";
                case string hn when hn.Equals("(A:Indicated Altitude,feet)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Indicated Altitude:1,feet)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Indicated Altitude:2,feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE\", \"feet\"))";
                case string hn when hn.Equals("(A:Variometer rate,knots)", StringComparison.InvariantCultureIgnoreCase):
                    //return "parseFloat(SimVar.GetSimVarValue(\"AIRCRAFT WIND Y\", \"knots\"))"; // TODO: fix rate
                    //return "parseFloat(SimVar.GetSimVarValue(\"ACCELERATION BODY Y\", \"feet per second squared\"), 0);";
                    return "parseFloat(SimVar.GetSimVarValue(\"VERTICAL SPEED\", \"feet per minute\"), 0) / 101 + parseFloat(SimVar.GetSimVarValue(\"ACCELERATION WORLD Z\", \"feet per minute squared\"), 0)";
                case string hn when hn.Equals("(A:RADIO HEIGHT,meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"RADIO HEIGHT\", \"meters\"))";
                case string hn when hn.Equals("(A:RADIO HEIGHT,feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"RADIO HEIGHT\", \"feet\"))";
                case string hn when hn.Equals("(A:DECISION HEIGHT,feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"DECISION HEIGHT\", \"feet\"))";
                case string hn when hn.Equals("(A:DECISION HEIGHT,meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"DECISION HEIGHT\", \"meters\"))";
                case string hn when hn.Equals("(A:G force,G force)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:G force,Gforce)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"G FORCE\", \"GForce\"))";
                case string hn when hn.Equals("(A:Min G force,Gforce)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Min G force,G force)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"MIN G FORCE\", \"GForce\"))";
                case string hn when hn.Equals("(A:MaxG force,Gforce)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:MaxG force,G force)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"MAX G FORCE\", \"GForce\"))";


                // ATTITUDE
                case string hn when hn.Equals("(A:TURN COORDINATOR BALL,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "Math.min(30, Math.max(-30 , -parseFloat(SimVar.GetSimVarValue(\"PLANE BANK DEGREES\", \"degree\")))) / 30 * 100";
                case string hn when hn.Equals("(A:Delta Heading Rate,rpm)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(-SimVar.GetSimVarValue(\"TURN INDICATOR RATE\", \"degree per second\")) * 60 / 360";
                case string hn when hn.Equals("(A:Wiskey compass indication degrees,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Magnetic compass,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:PLANE HEADING DEGREES MAGNETIC,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES MAGNETIC\", \"degrees\"))";
                case string hm when hm.Equals("(A:Wiskey compass indication degrees,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hn when hn.Equals("(A:Magnetic compass,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:PLANE HEADING DEGREES MAGNETIC,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES MAGNETIC\", \"radians\"))";
                case string hn when hn.Equals("(A:Plane heading degrees gyro,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:heading indicator,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hm when hm.Equals("(A:heading indicator:1,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:heading indicator:2,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES TRUE\", \"degrees\"))";
                case string hn when hn.Equals("(A:PLANE HEADING DEGREES GYRO,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:heading indicator,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hm when hm.Equals("(A:heading indicator:1,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:heading indicator:2,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES TRUE\", \"radians\"))";
                case string hn when hn.Equals("(A:Autopilot heading lock dir,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT HEADING LOCK DIR\", \"degrees\"))";
                case string hn when hn.Equals("(A:RUDDER TRIM PCT,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"RUDDER TRIM PCT\", \"percent\"))";
                case string hn when hn.Equals("(A:ELEVATOR TRIM POSITION,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELEVATOR TRIM PCT\", \"percent\"))";
                case string hn when hn.Equals("(A:Attitude indicator bank degrees,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:PLANE BANK DEGREES,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE BANK DEGREES\", \"radians\"))";
                case string ho when ho.Equals("(A:PLANE BANK DEGREES,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE BANK DEGREES\", \"degree\"))";
                case string hn when hn.Equals("(A:Attitude indicator bank degrees,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE BANK DEGREES\", \"degree\"))";
                case string hn when hn.Equals("(A:Attitude indicator bank degrees:1,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE BANK DEGREES\", \"degree\"))";
                case string hn when hn.Equals("(A:Attitude indicator bank degrees,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Attitude indicator bank degrees:1,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Attitude indicator bank degrees:2,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE BANK DEGREES\", \"radians\"))";
                case string hn when hn.Equals("(A:Attitude indicator pitch degrees,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Attitude indicator pitch degrees:1,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Attitude indicator pitch degrees:2,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hq when hq.Equals("(A:PLANE PITCH DEGREES,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE PITCH DEGREES\", \"degree\"))";
                case string hn when hn.Equals("(A:PLANE HEADING DEGREES GYRO,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Plane heading degrees true,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES TRUE\", \"radians\"))";

                // AMBIENT
                case string hn when hn.Equals("(A:AMBIENT TEMPERATURE,Celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Total air temperature,Celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AMBIENT TEMPERATURE\", \"celsius\"))";
                case string hn when hn.Equals("(A:Ambient Wind Direction,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AMBIENT WIND DIRECTION\", \"degree\"))";
                case string hn when hn.Equals("(A:Ambient Wind VELOCITY,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AMBIENT WIND VELOCITY\", \"knots\"))";
                case string hn when hn.Equals("(A:Ambient Wind VELOCITY,kilometers per hour)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AMBIENT WIND VELOCITY\", \"kilometers per hour\"))";
                case string hn when hn.Equals("(A:Ambient Wind VELOCITY,mph)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AMBIENT WIND VELOCITY\", \"mph\"))";
                case string hn when hn.Equals("(A:AMBIENT VISIBILITY,nmiles)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AMBIENT VISIBILITY\", \"nautical mile\"))";
                case string hn when hn.Equals("(A:Kohlsman setting hg,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"KOHLSMAN SETTING HG\", \"inches of mercury\"))";
                case string ho when ho.Equals("(A:Kohlsman setting hg:1,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"KOHLSMAN SETTING HG:1\", \"inches of mercury\"))";
                case string ho when ho.Equals("(A:Kohlsman setting hg:2,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"KOHLSMAN SETTING HG:2\", \"inches of mercury\"))";
                case string hn when hn.Equals("(A:Kohlsman setting hg,mbar)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"KOHLSMAN SETTING HG\", \"Millibars\"))";
                case string ho when ho.Equals("(A:Kohlsman setting hg:1,mbar)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"KOHLSMAN SETTING HG:1\", \"Millibars\"))";
                case string ho when ho.Equals("(A:Kohlsman setting hg:2,mbar)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"KOHLSMAN SETTING HG:2\", \"Millibars\"))";

                case string hn when hn.Equals("(P:Local time,seconds)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"LOCAL TIME\", \"seconds\"))";
                case string hn when hn.Equals("(P:Local time,minutes)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"LOCAL TIME\", \"minutes\"))";
                case string hn when hn.Equals("(P:Local time,hours)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"LOCAL TIME\", \"hours\"))";
                case string hn when hn.Equals("(L:Zulu,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "0";
                case string hn when hn.Equals("(P:ZULU TIME,hours)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"ZULU TIME\", \"hours\"))";
                case string hn when hn.Equals("(P:ZULU TIME,minutes)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"ZULU TIME\", \"minutes\"))";
                case string hn when hn.Equals("(P:Absolute time,seconds)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"E: ABSOLUTE TIME\", \"seconds\"))";

                    // FUEL
                case string hl when hl.Equals("(A:Fuel weight per gallon,pounds)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL WEIGHT PER GALLON\", \"lbs\"))";

                case string hk when hk.Equals("(A:fuel tank left quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel left quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK LEFT QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:fuel tank left quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel left quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK LEFT QUANTITY\", \"gallons\"))";
                case string hk when hk.Equals("(A:fuel tank right quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel right quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK RIGHT QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:fuel tank right quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel right quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK RIGHT QUANTITY\", \"gallons\"))";

                case string hk when hk.Equals("(A:fuel tank left aux quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel left aux quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK LEFT AUX QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:fuel tank left aux quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel left aux quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK LEFT AUX QUANTITY\", \"gallons\"))";
                case string hk when hk.Equals("(A:fuel tank right aux quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel right aux quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK RIGHT AUX QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:fuel tank right aux quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel right aux quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK RIGHT AUX QUANTITY\", \"gallons\"))";

                case string hk when hk.Equals("(A:fuel tank left MAIN quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel left MAIN quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK LEFT MAIN QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:fuel tank left MAIN quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel left MAIN quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK LEFT MAIN QUANTITY\", \"gallons\"))";
                case string hk when hk.Equals("(A:fuel tank right MAIN quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel right MAIN quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK RIGHT MAIN QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:fuel tank right MAIN quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel right MAIN quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK RIGHT MAIN QUANTITY\", \"gallons\"))";

                case string hk when hk.Equals("(A:fuel tank left tip quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel left tip quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK LEFT TIP QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:fuel tank left tip quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel left tip quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK LEFT TIP QUANTITY\", \"gallons\"))";
                case string hk when hk.Equals("(A:fuel tank right tip quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel right tip quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK RIGHT TIP QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:fuel tank right tip quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel right tip quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK RIGHT TIP QUANTITY\", \"gallons\"))";

                case string hk when hk.Equals("(A:fuel tank center quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel center quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK CENTER QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:fuel tank center quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:fuel center quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK CENTER QUANTITY\", \"gallons\"))";
                case string hk when hk.Equals("(A:Fuel tank center level,position)", StringComparison.InvariantCultureIgnoreCase):
                case string hl when hl.Equals("(A:Fuel center level,position)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TANK CENTER QUANTITY\", \"gallons\")) / parseFloat(SimVar.GetSimVarValue(\"FUEL TANK CENTER CAPACITY\", \"gallons\"))";

                case string hk when hk.Equals("(A:Fuel total quantity,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL QUANTITY\", \"gallons\"))";
                case string hk when hk.Equals("(A:Fuel total quantity,liter)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL QUANTITY\", \"liters\"))";
                case string hk when hk.Equals("(A:FUEL TOTAL CAPACITY,gallons)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL CAPACITY\", \"gallons\"))";
                case string hk when hk.Equals("(A:FUEL TOTAL CAPACITY,liters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL CAPACITY\", \"liters\"))";

                case string hn when hn.Equals("(A:Fuel tank total level,position)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL QUANTITY\", \"gallons\")) / parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL CAPACITY\", \"gallons\"))";

                // ENGINES
                case string hn when hn.Equals("(A:SUCTION PRESSURE,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"SUCTION PRESSURE\", \"inch of mercury\"))";
                case string hn when hn.Equals("(A:GENERAL ENG1 OIL PRESSURE,PSI)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng1 Oil Pressure,PSI)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:1\", \"psi\"))";
                case string hn when hn.Equals("(A:GENERAL ENG2 OIL PRESSURE,PSI)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng2 Oil Pressure,PSI)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:2\", \"psi\"))";
                case string hn when hn.Equals("(A:GENERAL ENG3 OIL PRESSURE,PSI)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng3 Oil Pressure,PSI)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:3\", \"psi\"))";
                case string hn when hn.Equals("(A:GENERAL ENG4 OIL PRESSURE,PSI)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng4 Oil Pressure,PSI)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:4\", \"psi\"))";
                case string hn when hn.Equals("(A:GENERAL ENG1 OIL PRESSURE,PSF)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng1 Oil Pressure,PSF)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:1\", \"psf\"))";
                case string hn when hn.Equals("(A:GENERAL ENG2 OIL PRESSURE,PSF)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng2 Oil Pressure,PSF)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:2\", \"psf\"))";
                case string hn when hn.Equals("(A:GENERAL ENG3 OIL PRESSURE,PSF)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng3 Oil Pressure,PSF)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:3\", \"psf\"))";
                case string hn when hn.Equals("(A:GENERAL ENG4 OIL PRESSURE,PSF)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng4 Oil Pressure,PSF)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE:4\", \"psf\"))";
                case string hj when hj.Equals("(A:ENG1 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG1 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL TEMPERATURE:1\", \"celsius\"))";
                case string hj when hj.Equals("(A:ENG2 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG2 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL TEMPERATURE:2\", \"celsius\"))";
                case string hj when hj.Equals("(A:ENG3 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG3 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL TEMPERATURE:3\", \"celsius\"))";
                case string hj when hj.Equals("(A:ENG4 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG4 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL TEMPERATURE:4\", \"celsius\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:1,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:1\", \"psi\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:2,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:2\", \"psi\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:3,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:3\", \"psi\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:4,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:4\", \"psi\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:1,PSF)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:1\", \"PSF\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:2,PSF)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:2\", \"PSF\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:3,PSF)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:3\", \"PSF\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:4,PSF)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:4\", \"PSF\"))";
                case string hn when hn.Equals("(A:GENERAL ENG1 RPM,rpm)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG RPM:2\", \"Rpm\"))";
                case string hn when hn.Equals("(A:GENERAL ENG2 RPM,rpm)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG RPM:1\", \"Rpm\"))";
                case string hn when hn.Equals("(A:ENG1 MANIFOLD PRESSURE,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG MANIFOLD PRESSURE:1\", \"inHG\"))";
                case string hn when hn.Equals("(A:ENG2 MANIFOLD PRESSURE,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG MANIFOLD PRESSURE:2\", \"inHG\"))";
                case string hn when hn.Equals("(A:RECIP CARBURETOR TEMPERATURE:1,celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG1 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG OIL TEMPERATURE:1\", \"celsius\")) * 0.5";//??
                case string hn when hn.Equals("(A:RECIP CARBURETOR TEMPERATURE:2,celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG2 OIL TEMPERATURE,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG OIL TEMPERATURE:2\", \"celsius\")) * 0.5";//??
                case string hn when hn.Equals("(A:RECIP ENG CYLINDER HEAD TEMPERATURE:1,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG CYLINDER HEAD TEMPERATURE:1\", \"celsius\"))";
                case string hn when hn.Equals("(A:RECIP ENG CYLINDER HEAD TEMPERATURE:2,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG CYLINDER HEAD TEMPERATURE:2\", \"celsius\"))";
                case string hn when hn.Equals("(A:Eng1 oil quantity,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL QUANTITY:1\", \"percent\"))";
                case string hn when hn.Equals("(A:Eng2 oil quantity,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL QUANTITY:2\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng1 N1,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng1 N1 RPM,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N1 RPM:1\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng2 N1,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng2 N1 RPM,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N1 RPM:2\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng3 N1,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng3 N1 RPM,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N1 RPM:3\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng4 N1,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng4 N1 RPM,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N1 RPM:4\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng1 N2,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng1 N2 RPM,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM:1\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng2 N2,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng2 N2 RPM,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM:2\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng3 N2,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng3 N2 RPM,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM:3\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb eng4 N2,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng4 N2 RPM,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM:4\", \"percent\"))";
                case string hn when hn.Equals("(A:Turb Eng1 Fuel Flow PPH,pounds per hour)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng1 Fuel Flow PPH,pounds per hour)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG FUEL FLOW PPH:1\", \"Pounds per hour\"))";
                case string hn when hn.Equals("(A:Turb Eng2 Fuel Flow PPH,pounds per hour)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng2 Fuel Flow PPH,pounds per hour)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG FUEL FLOW PPH:2\", \"Pounds per hour\"))";
                case string hn when hn.Equals("(A:Turb Eng3 Fuel Flow PPH,pounds per hour)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng3 Fuel Flow PPH,pounds per hour)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG FUEL FLOW PPH:3\", \"Pounds per hour\"))";
                case string hn when hn.Equals("(A:Turb Eng4 Fuel Flow PPH,pounds per hour)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng4 Fuel Flow PPH,pounds per hour)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG FUEL FLOW PPH:4\", \"Pounds per hour\"))";
                case string hn when hn.Equals("(A:TURB ENG1 N1,Percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TURB ENG N1:1\", \"percent\"))";
                case string hn when hn.Equals("(A:TURB ENG2 N1,Percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TURB ENG N1:2\", \"percent\"))";
                case string hn when hn.Equals("(A:TURB ENG3 N1,Percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TURB ENG N1:3\", \"percent\"))";
                case string hn when hn.Equals("(A:TURB ENG4 N1,Percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TURB ENG N1:4\", \"percent\"))";
                case string hn when hn.Equals("(A:TURB ENG1 N1,part)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TURB ENG N1:1\", \"part\"))";
                case string hn when hn.Equals("(A:TURB ENG2 N1,part)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TURB ENG N1:2\", \"part\"))";
                case string hn when hn.Equals("(A:TURB ENG3 N1,part)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TURB ENG N1:3\", \"part\"))";
                case string hn when hn.Equals("(A:TURB ENG4 N1,part)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TURB ENG N1:4\", \"part\"))";
                case string hn when hn.Equals("(A:general eng1 throttle lever position,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG THROTTLE LEVER POSITION:1\", \"percent\"))";
                case string hn when hn.Equals("(A:general eng2 throttle lever position,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG THROTTLE LEVER POSITION:2\", \"percent\"))";
                case string hn when hn.Equals("(A:general eng3 throttle lever position,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG THROTTLE LEVER POSITION:3\", \"percent\"))";
                case string hn when hn.Equals("(A:general eng4 throttle lever position,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG THROTTLE LEVER POSITION:4\", \"percent\"))";
                case string hn when hn.Equals("(A:TURB ENG1 corrected N2,Percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM: 1\", \"percent\"))";
                case string hn when hn.Equals("(A:TURB ENG2 corrected N2,Percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM: 2\", \"percent\"))";
                case string hn when hn.Equals("(A:TURB ENG3 corrected N2,Percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM: 3\", \"percent\"))";
                case string hn when hn.Equals("(A:TURB ENG4 corrected N2,Percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG N2 RPM: 4\", \"percent\"))";
                case string hn when hn.Equals("(A:Hydraulic1 Pressure,psi)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng1 hydraulic pressure,psi)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Eng hydraulic pressure:1,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG HYDRAULIC PRESSURE: 1\", \"psi\"))";
                case string hn when hn.Equals("(A:Hydraulic2 Pressure,psi)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng2 hydraulic pressure,psi)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Eng hydraulic pressure:2,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG HYDRAULIC PRESSURE: 2\", \"psi\"))";
                case string hn when hn.Equals("(A:Hydraulic3 Pressure,psi)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng3 hydraulic pressure,psi)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Eng hydraulic pressure:3,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG HYDRAULIC PRESSURE: 3\", \"psi\"))";
                case string hn when hn.Equals("(A:Hydraulic4 Pressure,psi)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Eng4 hydraulic pressure,psi)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:Eng hydraulic pressure:4,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG HYDRAULIC PRESSURE: 4\", \"psi\"))";
                case string hn when hn.Equals("(A:General eng1 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG EXHAUST GAS TEMPERATURE: 1\", \"celsius\"))";
                case string hn when hn.Equals("(A:eng1 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG EXHAUST GAS TEMPERATURE:1\", \"celsius\"))";
                case string hn when hn.Equals("(A:General eng2 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG EXHAUST GAS TEMPERATURE: 2\", \"celsius\"))";
                case string hn when hn.Equals("(A:eng2 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG EXHAUST GAS TEMPERATURE:2\", \"celsius\"))";
                case string hn when hn.Equals("(A:General eng3 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG EXHAUST GAS TEMPERATURE: 3\", \"celsius\"))";
                case string hn when hn.Equals("(A:eng3 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG EXHAUST GAS TEMPERATURE:3\", \"celsius\"))";
                case string hn when hn.Equals("(A:General eng4 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG EXHAUST GAS TEMPERATURE: 4\", \"celsius\"))";
                case string hn when hn.Equals("(A:eng4 exhaust gas temperature,celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG EXHAUST GAS TEMPERATURE:4\", \"celsius\"))";
                case string hn when hn.Equals("(A:Turb eng1 vibration,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Turb vibration:1,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG VIBRATION:1\", \"Number\")) / 0.05";
                case string hn when hn.Equals("(A:Turb eng2 vibration,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Turb vibration:2,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG VIBRATION:2\", \"Number\")) / 0.05";
                case string ho when ho.Equals("(A:Turb eng1 pressure ratio,ratio)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG PRESSURE RATIO:1\", \"ratio\"))";
                case string ho when ho.Equals("(A:Turb eng2 pressure ratio,ratio)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG PRESSURE RATIO:2\", \"ratio\"))";
                case string ho when ho.Equals("(A:Turb eng3 pressure ratio,ratio)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG PRESSURE RATIO:3\", \"ratio\"))";
                case string ho when ho.Equals("(A:Turb eng4 pressure ratio,ratio)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG PRESSURE RATIO:4\", \"ratio\"))";

                // NAV
                case string hn when hn.Equals("(A:NAV GSI:1,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV GSI:1\", \"percent\"))";
                case string hn when hn.Equals("(A:NAV GSI:2,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV GSI:2\", \"percent\"))";
                case string hn when hn.Equals("(A:NAV1 OBS,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:NAV1 radial,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:1\", \"degrees\"))";
                case string hn when hn.Equals("(A:NAV2 OBS,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:NAV2 radial,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:2\", \"degrees\"))";
                case string hn when hn.Equals("(A:NAV1 OBS,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:NAV1 radial,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:1\", \"radians\"))";
                case string hn when hn.Equals("(A:NAV2 OBS,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:NAV2 radial,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:2\", \"radians\"))";
                case string hn when hn.Equals("(A:GPS WP TRUE BEARING,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GPS WP BEARING\", \"degree\"))";
                case string hn when hn.Equals("(A:GPS MAGVAR,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"MAGVAR\", \"degrees\"))";
                case string hn when hn.Equals("(A:NAV1 DME,nmiles)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV DME\", \"nautical miles\"))";
                case string hn when hn.Equals("(A:GPS WP DISTANCE,nmiles)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GPS WP DISTANCE)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GPS WP DISTANCE\", \"nautical mile\"))";
                case string hn when hn.Equals("(A:PLANE LONGITUDE,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE LONGITUDE\", \"radians\"))";
                case string hn when hn.Equals("(A:PLANE LATITUDE,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE LATITUDE\", \"radians\"))";
                case string hn when hn.Equals("(A:PLANE LONGITUDE,degree)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:PLANE LONGITUDE,degree longitude)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE LONGITUDE\", \"degree longitude\"))";
                case string hn when hn.Equals("(A:PLANE LATITUDE,degree)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:PLANE LATITUDE,degree latitude)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE LATITUDE\", \"degree latitude\"))";
                case string hn when hn.Equals("(A:GPS GROUND MAGNETIC TRACK,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GPS GROUND MAGNETIC TRACK\", \"degree\"))";
                case string ho when ho.Equals("(A:GPS WP BEARING,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GPS WP BEARING\", \"degree\"))";
                case string ho when ho.Equals("(A:GPS drives nav1,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GPS DRIVES NAV1\", \"Boolean\"))";
                case string ho when ho.Equals("(A:Nav1 Active Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV ACTIVE FREQUENCY:1\", \"MHz\"))";
                case string ho when ho.Equals("(A:Nav1 Active Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV ACTIVE FREQUENCY:1\", \"KHz\"))";
                case string ho when ho.Equals("(A:Nav2 Active Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV ACTIVE FREQUENCY:2\", \"MHz\"))";
                case string ho when ho.Equals("(A:Nav2 Active Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV ACTIVE FREQUENCY:2\", \"KHz\"))";
                case string ho when ho.Equals("(A:Nav1 Standby Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV STANDBY FREQUENCY:1\", \"MHz\"))";
                case string ho when ho.Equals("(A:Nav1 Standby Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV STANDBY FREQUENCY:1\", \"KHz\"))";
                case string ho when ho.Equals("(A:Nav2 Standby Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV STANDBY FREQUENCY:2\", \"MHz\"))";
                case string ho when ho.Equals("(A:Nav2 Standby Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV STANDBY FREQUENCY:2\", \"KHz\"))";

                // RADIO
                case string ho when ho.Equals("(A:Com1 Active Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"COM ACTIVE FREQUENCY:1\", \"MHz\"))";
                case string ho when ho.Equals("(A:Com1 Active Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"COM ACTIVE FREQUENCY:1\", \"KHz\"))";
                case string ho when ho.Equals("(A:Com2 Active Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"COM ACTIVE FREQUENCY:2\", \"MHz\"))";
                case string ho when ho.Equals("(A:Com2 Active Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"COM ACTIVE FREQUENCY:2\", \"KHz\"))";
                case string ho when ho.Equals("(A:Com1 Standby Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"COM STANDBY FREQUENCY:1\", \"MHz\"))";
                case string ho when ho.Equals("(A:Com1 Standby Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"COM STANDBY FREQUENCY:1\", \"KHz\"))";
                case string ho when ho.Equals("(A:Com2 Standby Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"COM STANDBY FREQUENCY:2\", \"MHz\"))";
                case string ho when ho.Equals("(A:Com2 Standby Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"COM STANDBY FREQUENCY:2\", \"KHz\"))";
                case string hn when hn.Equals("(A:ADF radial:1,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:ADF1 radial,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF RADIAL:1\", \"radians\"))";
                case string hn when hn.Equals("(A:ADF radial:2,radians)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:ADF2 radial,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF RADIAL:2\", \"radians\"))";
                case string hn when hn.Equals("(A:ADF radial:1,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:ADF1 radial,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF RADIAL:1\", \"degrees\"))";
                case string hn when hn.Equals("(A:ADF radial:2,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:ADF2 radial,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF RADIAL:2\", \"degrees\"))";
                case string ho when ho.Equals("(A:Adf1 Active Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:ADF active frequency:1,megahertz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF ACTIVE FREQUENCY:1\", \"MHz\"))";
                case string ho when ho.Equals("(A:Adf1 Active Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:ADF active frequency:1,kilohertz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF ACTIVE FREQUENCY:1\", \"KHz\"))";
                case string ho when ho.Equals("(A:Adf2 Active Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:ADF active frequency:2,megahertz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF ACTIVE FREQUENCY:2\", \"MHz\"))";
                case string ho when ho.Equals("(A:Adf2 Active Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                case string hp when hp.Equals("(A:ADF active frequency:2,kilohertz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF ACTIVE FREQUENCY:2\", \"KHz\"))";
                case string ho when ho.Equals("(A:Adf1 Standby Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF STANDBY FREQUENCY:1\", \"MHz\"))";
                case string ho when ho.Equals("(A:Adf1 Standby Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF STANDBY FREQUENCY:1\", \"KHz\"))";
                case string ho when ho.Equals("(A:Adf2 Standby Frequency,MHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF STANDBY FREQUENCY:2\", \"MHz\"))";
                case string ho when ho.Equals("(A:Adf2 Standby Frequency,KHz)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF STANDBY FREQUENCY:2\", \"KHz\"))";
                case string ho when ho.Equals("(A:Adf signal:1,number)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF SIGNAL:1\", \"number\"))";
                case string hp when hp.Equals("(A:Adf signal:2,number)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF SIGNAL:2\", \"number\"))";
                case string hp when hp.Equals("(A:ATC ID,string)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ATC ID\", \"string\"))";

                // CONTROLS
                case string ho when ho.Equals("(A:GEAR HANDLE POSITION,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GEAR HANDLE POSITION\", \"percent\"))";
                case string hn when hn.Equals("(A:INCIDENCE ALPHA,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"INCIDENCE ALPHA\", \"degrees\"))";
                case string hn when hn.Equals("(A:Autopilot heading lock dir,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT HEADING LOCK DIR\", \"radians\"))";
                case string hn when hn.Equals("(A:Autopilot heading lock dir,degree)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT HEADING LOCK DIR\", \"degree\"))";
                case string hn when hn.Equals("(A:AUTOPILOT FLIGHT DIRECTOR ACTIVE,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT FLIGHT DIRECTOR ACTIVE\", \"bool\"))";
                case string hn when hn.Equals("(A:Autopilot flight director bank,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT FLIGHT DIRECTOR BANK\", \"degree\"))";
                case string hn when hn.Equals("(A:Autopilot flight director bank,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT FLIGHT DIRECTOR BANK\", \"radians\"))";
                case string hn when hn.Equals("(A:Autopilot flight director pitch,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT FLIGHT DIRECTOR BANK\", \"degree\"))";
                case string hn when hn.Equals("(A:Autopilot flight director pitch,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT FLIGHT DIRECTOR BANK\", \"radians\"))";
                case string hn when hn.Equals("(A:CG Percent,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"CG PERCENT\", \"percent\"))";
                case string hn when hn.Equals("(A:Pressurization Pressure Differential,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PRESSURIZATION PRESSURE DIFFERENTIAL\", \"psi\"))";
                case string hn when hn.Equals("(A:Pressurization Cabin Altitude,feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PRESSURIZATION CABIN ALTITUDE\", \"feet\"))";
                case string hn when hn.Equals("(A:Pressurization Cabin Altitude,meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PRESSURIZATION CABIN ALTITUDE\", \"meters\"))";
                case string hn when hn.Equals("(A:Pressurization Cabin Altitude Rate,feet per minute)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PRESSURIZATION CABIN ALTITUDE RATE\", \"feet per minute\"))";
                case string hn when hn.Equals("(A:Pressurization Cabin Altitude Rate,meters per second)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PRESSURIZATION CABIN ALTITUDE RATE\", \"meters per second\"))";
                case string hn when hn.Equals("(A:Aileron trim pct,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AILERON TRIM PCT\", \"percent\"))";
                case string hn when hn.Equals("(A:MAGVAR,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"MAGVAR\", \"degree\"))";
                case string hn when hn.Equals("(A:Trailing edge flaps0 left angle,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Trailing edge flaps1 left angle,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TRAILING EDGE FLAPS LEFT ANGLE\", \"degrees\"))";
                case string hn when hn.Equals("(A:Trailing edge flaps0 left angle,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Trailing edge flaps1 left angle,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TRAILING EDGE FLAPS LEFT ANGLE\", \"percent\"))";
                case string hn when hn.Equals("(A:TRAILING EDGE FLAPS LEFT ANGLE,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TRAILING EDGE FLAPS LEFT ANGLE\", \"radians\"))";
                case string hn when hn.Equals("(A:Trailing edge flaps0 right angle,degrees)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Trailing edge flaps1 right angle,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TRAILING EDGE FLAPS RIGHT ANGLE\", \"degrees\"))";
                case string hn when hn.Equals("(A:Trailing edge flaps0 right angle,percent)", StringComparison.InvariantCultureIgnoreCase):
                case string ho when ho.Equals("(A:Trailing edge flaps1 right angle,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TRAILING EDGE FLAPS RIGHT ANGLE\", \"percent\"))";
                case string hn when hn.Equals("(A:TRAILING EDGE FLAPS RIGHT ANGLE,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TRAILING EDGE FLAPS RIGHT ANGLE\", \"radians\"))";

                // ELECTRICS
                case string hn when hn.Equals("(L:Show Volts 1,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";//"parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL MAIN BUS VOLTAGE:1\", \"volts\"))";
                case string hn when hn.Equals("(L:Show Volts 2,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";//parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL MAIN BUS VOLTAGE:2\", \"volts\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS VOLTAGE:1,volts)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS VOLTAGE:1\", \"volts\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS VOLTAGE:2,volts)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS VOLTAGE:2\", \"volts\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS AMPS:1,amps)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS AMPS:1\", \"amperes\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS AMPS:2,amps)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS AMPS:2\", \"amperes\"))";
                case string hn when hn.Equals("(A:ELECTRICAL BATTERY BUS AMPS,amps)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL BATTERY BUS AMPS\", \"amperes\"))";
                case string hn when hn.Equals("(A:ELECTRICAL BATTERY BUS VOLTAGE,volts)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL BATTERY BUS VOLTAGE\", \"volts\"))";

                case string hn when hn.Equals("(A:Sim On Ground,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"SIM ON GROUND\", \"boolean\"))";

                /*
                (A:Aileron Position,degrees)
                (A:Airspeed Barber Pole,knots)
                (A:Apu On Fire Detected,bool)
                (A:Attitude bars position,position)
                (A:Auto brake switch cb,enum)
                (A:BARBER POLE MACH,mach)
                (A:Brake Dependent Hydraulic Pressure,PSF)
                (A:BRAKE INDICATOR,enum)
                (A:Elevator position,degrees)
                (A:Eng1 On Fire,bool)
                (A:Eng2 On Fire,bool)
                (A:Eng3 On Fire,bool)
                (A:Eng4 On Fire,bool)
                (A:Gear Warning,bool)
                (A:General Eng Generator Active:1,bool)
                (A:General Eng Generator Active:2,bool)
                (A:General Eng Generator Active:3,bool)
                (A:General Eng Generator Active:4,bool)
                (A:General eng1 anti ice position,bool)
                (A:HSI CDI needle valid,bool)
                (A:HSI CDI needle,number)
                (A:HSI GSI needle valid,bool)
                (A:HSI GSI needle,number)
                (A:INCIDENCE ALPHA,number)
                (A:Light Panel,bool)
                (A:Rudder Position,degrees)
                (A:STALL ALPHA,number)
                (A:Turb Eng1 Afterburner,bool)

                (A:Electrical avionics bus voltage,volts)
                (A:ELECTRICAL GENALT1 BUS AMPS,amps)
                (A:ELECTRICAL GENALT2 BUS AMPS,amps)
                (A:Electrical main bus voltage,volts)
                (A:Electrical total load amps,amps)

                (A:HSI CDI needle valid,bool)
                (A:HSI CDI NEEDLE VALID:1,bool)
                (A:HSI CDI needle,number)
                (A:HSI distance,nmiles)
                (A:HSI GSI needle valid,bool)
                (A:HSI GSI needle,number)
                (A:HSI has localizer,bool)
                (A:HSI TF FLAGS,enum)

                (A:NAV GS FLAG: 2,bool)
                (A:NAV HAS GLIDE SLOPE,bool)
                (A:NAV HAS NAV: 2,bool)
                (A:NAV TOFROM: 2,enum)
                (A:Nav1 ident,string)
                (A:NAV2 CDI,number)
                (A:NAV2 DME,kilometers)
                (A:NAV2 DME,nmiles)
                (A:NAV2 GSI,number)
                (A:NAV2 HAS NAV,bool)
                (A:Nav2 ident,string)

                (A:Autopilot airspeed hold var,knots)
                (A:Autopilot altitude lock var,feet)
                (A:Autopilot mach hold var,mach)
                (A:Autopilot vertical hold var,feet per minute)
                (A:Autopilot airspeed hold,bool)
                (A:Autopilot altitude lock var,feet)
                (A:Autopilot altitude lock var,meters)
                (A:Autopilot altitude lock,bool)
                (A:AUTOPILOT APPROACH HOLD,bool)
                (A:AUTOPILOT BACKCOURSE HOLD,bool)
                (A:Autopilot Glideslope Hold,bool)
                (A:Autopilot heading lock,bool)
                (A:Autopilot master,bool)
                (A:Autopilot nav1 lock,bool)
                (A:Autopilot takeoff power active,bool)
                (A:AUTOPILOT THROTTLE ARM,bool)
                (A:AUTOPILOT VERTICAL HOLD VAR,meters per minute)
                (A:AUTOTHROTTLE ACTIVE,bool)

                (A:APU PCT RPM,part)
                (A:APU PCT RPM,percent over 100)
                (A:APU Volts,volts)
                (A:Auto brake switch cb,enum)
                (A:BRAKE LEFT POSITION,percent)
                (A:BRAKE RIGHT POSITION,percent)
                (A:ELEVATOR POSITION,degrees)
                (A:Eng1 fuel flow PPH: @ENGINE_NUMBER,pounds per hour)
                (A:Eng2 fuel flow PPH: @ENGINE_NUMBER,pounds per hour)
                (A:FLAPS HANDLE PERCENT,percent)
                (A:fly by wire alpha protection,bool)
                (A:Gear left position,percent)
                (A:Inner Marker,bool)
                (A:Launchbar Position,percent)
                (A:Middle Marker,bool)
                (A:Outer Marker,bool)
                (A:SIM ON GROUND,bool)
                (A:TOTAL AIR TEMPERATURE,Celsius)
                (A:Turb Eng Max Torque Percent:1,part)
                (A:Turb Eng Max Torque Percent:2,part)
                (A:Turn coordinator ball,position)

                */

                // TRY TO COPY 1IN1
                default:
                    XmlHelper.writeLog("FSX variable not found: " + fsxVar);

                    if (fsxVar.StartsWith("(L:"))
                        return "1";

                    fsxVar = Regex.Replace(fsxVar, @"(\([A-Za-z]:)|\(|\)", "");

                    if (fsxVar.Contains(",") && fsxVar.Split(',').Length == 2)
                    {
                        string varname = fsxVar.Split(',')[0].Trim();
                        string unit = fsxVar.Split(',')[1].Trim().ToLower();
                        if (unit == "m/s")
                            unit = "meter per second";
                        else if (unit == "nmiles")
                            unit = "nautical miles";
                        else if (unit == "bool")
                            unit = "boolean";
                        else if (unit == "kilohertz")
                            unit = "KHz";
                        else if (unit == "megahertz")
                            unit = "MHz";
                        else if (unit == "pounds" || unit == "pound")
                            unit = "lbs";
                        else if (unit == "mbars" || unit == "mbar")
                            unit = "Millibars";

                        string converted = "parseFloat(SimVar.GetSimVarValue(\"" + varname + "\", \"" + unit + "\"))";
                        XmlHelper.writeLog("Conversion result: " + converted);
                        return converted;
                    }

                    return "0";
            }
        }

        // POSTFIX CONVERTER BY W. Michael Perkins STARTS
        public class Intermediate
        {
            public string expr;     // subexpression string
            public string oper;     // the operator used to create this expression

            public Intermediate(string expr, string oper)
            {
                this.expr = expr;
                this.oper = oper;
            }
        }

        public string PostfixToInfix(string postfix, xmlHelper XmlHelper)
        {
            try
            {
                Stack<int> elseCounter = new Stack<int>();
                Stack<Intermediate> ifCond = new Stack<Intermediate>();

                Intermediate[] stackRegister = new Intermediate[50];

                postfix = postfix.Replace("if {", "if{").Replace("} els", "}els").Replace("els {", "els{").Trim();

                // CHECK TRAILING BRACKET BUG
                //if (postfix.Length > 0 && postfix[postfix.Length - 1] == '}' && postfix.Count(f => f == '{') < postfix.Count(f => f == '}'))
                if (postfix.Length > 0 && postfix[postfix.Length - 1] == '}' && ((postfix.Split('{').Length - 1) < (postfix.Split('}').Length - 1)))
                    postfix = postfix.TrimEnd('}').Trim();

                // PROCESS STACKS
                var postfixTokens = postfix.Split(' ');

                string lastToken = "";

                var stack = new Stack<Intermediate>();
                var backupStack = new Stack<Intermediate>();

                foreach (string token in postfixTokens)
                {
                    lastToken = token;

                    string newExpr;

                    XmlHelper.writeLog("Current token: " + token);

                    if (token == "SETVARPLACEHOLDER") // SET VARIABLE GAP
                    {
                        stack.Pop();
                        stack.Push(new Intermediate(token, token));
                    }
                    // s0 - Stores the top value in an internal register, but does not pop it from the stack.
                    else if (Regex.IsMatch(token, @"^s[-0-9.]+$"))
                    {
                        if (int.TryParse(token.Replace("s", ""), out int num))
                        {
                            stackRegister[num] = stack.Peek();
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to save stack " + token);
                            return "";
                        }
                    }
                    // l0 - Loads a value from a register to the top of the stack
                    else if (Regex.IsMatch(token, @"^l[-0-9.]+$"))
                    {
                        if (int.TryParse(token.Replace("l", ""), out int num) && stackRegister[num] != null)
                        {
                            stack.Push(stackRegister[num]);
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to load stack " + token);
                            return "";
                        }
                    }
                    // sp0 - Loads a value from a register to the top of the stack
                    else if (Regex.IsMatch(token, @"^sp[-0-9.]+$"))
                    {
                        if (int.TryParse(token.Replace("sp", ""), out int num))
                        {
                            stackRegister[num] = stack.Pop();
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to popsave stack " + token);
                            return "";
                        }
                    }
                    // Pops and discards the top value on the stack
                    else if (token.ToLower() == "p")
                        if (stack.Count > 0)
                        {
                            stack.Pop();
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    // Backup the stack
                    else if (token.ToLower() == "b")
                        stack = backupStack;
                    // Clears the stack
                    else if (token.ToLower() == "c")
                        stack = new Stack<Intermediate>();
                    // Duplicates the value that is on the top of the stack
                    else if (token.ToLower() == "d")
                        if (stack.Count > 0)
                        {
                            stack.Push(stack.Peek());
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    // Reverses the top and second values on the stack
                    else if (token.ToLower() == "r")
                    {
                        if (stack.Count > 1)
                        {
                            Intermediate first = stack.Pop();
                            Intermediate second = stack.Pop();
                            stack.Push(first);
                            stack.Push(second);
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token == "==" || token == "!=" || token == ">" || token == "<" || token == ">=" || token == "<=")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "( " + leftIntermediate.expr + " " + token + " " + rightIntermediate.expr + " )";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token == "?")
                    {
                        if (stack.Count > 2)
                        {
                            var check = stack.Pop();
                            var opt2 = stack.Pop();
                            var opt1 = stack.Pop();
                            newExpr = " ( " + check.expr + " ? " + opt1.expr + " : " + opt2.expr + " ) ";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "if{")
                    {
                        elseCounter.Push(0);

                        if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = "( " + rightIntermediate.expr + " ? ";
                            //stack.Push(new Intermediate(newExpr, token));
                            ifCond.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "}els{")
                    {
                        elseCounter.Pop();
                        elseCounter.Push(1);

                        if (stack.Count > 0 && ifCond.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = ifCond.Pop().expr + " " + rightIntermediate.expr + " : ";
                            ifCond.Push(new Intermediate(newExpr, token));
                        }
                        else if (ifCond.Count > 0)
                        {
                            newExpr = ifCond.Pop().expr;
                            ifCond.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token == "}")
                    {
                        if (elseCounter.Peek() == 1 && ifCond.Count > 0)
                        {
                            if (stack.Count > 0)
                            {
                                var rightIntermediate = stack.Pop();
                                newExpr = ifCond.Pop().expr + " " + rightIntermediate.expr + " )";
                                stack.Push(new Intermediate(newExpr, token));
                            } else
                            {
                                newExpr = ifCond.Pop().expr + " )";
                                stack.Push(new Intermediate(newExpr, token));
                            }
                        }
                        else if (elseCounter.Peek() == 0 && ifCond.Count > 0)
                        {
                            if (stack.Count > 0)
                            {
                                var rightIntermediate = stack.Pop();
                                newExpr = ifCond.Pop().expr + " " + rightIntermediate.expr + " : 0 )";
                                stack.Push(new Intermediate(newExpr, token));
                            } else
                            {
                                newExpr = ifCond.Pop().expr + " : 0 )";
                                stack.Push(new Intermediate(newExpr, token));
                            }
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token + " (elseCount: " + elseCounter.Peek() + ")");
                            return "";
                        }

                        elseCounter.Pop();
                    }
                    else if (token == "&&" || token.ToLower() == "and")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + " && " + rightIntermediate.expr;
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token == "||" || token.ToLower() == "or")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "( " + leftIntermediate.expr + " || " + rightIntermediate.expr + " )";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token == "!" || token.ToLower() == "not")
                    {
                        var leftIntermediate = stack.Pop();
                        stack.Push(new Intermediate("!(" + leftIntermediate.expr + ")", token));
                    }
                    else if (token == "/-/" || token.ToLower() == "neg")
                    {
                        if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = "(" + rightIntermediate.expr + ") * (-1)";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token == "++")
                    {
                        if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = "(( " + rightIntermediate.expr + " ) + 1)";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token == "--")
                    {
                        if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = "(( " + rightIntermediate.expr + " ) - 1)";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token == "+" || token == "-" || token == "scat")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + " " + (token != "scat" ? token : "+") + " " + rightIntermediate.expr;
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token == "*" || token == "/" || token == "%" || token == "&" || token == "|" || token == "^" || token == ">>" || token == "<<")
                    {
                        string leftExpr, rightExpr;

                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            if (rightIntermediate.oper == "+" || rightIntermediate.oper == "-")
                            {
                                rightExpr = "(" + rightIntermediate.expr + ")";
                            }
                            else
                            {
                                rightExpr = rightIntermediate.expr;
                            }

                            var leftIntermediate = stack.Pop();
                            if (leftIntermediate.oper == "+" || leftIntermediate.oper == "-")
                            {
                                leftExpr = "(" + leftIntermediate.expr + ")";
                            }
                            else
                            {
                                leftExpr = leftIntermediate.expr;
                            }

                            newExpr = leftExpr + " " + token + " " + rightExpr;
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "quit")
                    {
                        break;
                    }
                    else if (token.ToLower() == "div")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "Math.floor( parseInt(" + leftIntermediate.expr + ") / parseInt(" + rightIntermediate.expr + ") )";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "lg")
                    {
                        if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = "Math.log(" + rightIntermediate.expr + ") / Math.log(10)";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "log")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "Math.log(" + leftIntermediate.expr + ") / Math.log(" + rightIntermediate.expr + ")";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "ctg")
                    {
                        if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = "1 / Math.tan(" + rightIntermediate.expr + ")";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    // g0
                    // case
                    else if (token.ToLower() == "rng")
                    {
                        if (stack.Count > 2)
                        {
                            var compare = stack.Pop();
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "( " + leftIntermediate.expr + " <= " + compare.expr + " && " + compare.expr + " <= " + rightIntermediate.expr + " )";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "schr")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "(" + leftIntermediate.expr + ").indexOf(String.fromCharCode(" + rightIntermediate.expr + "))";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "sstr")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "(" + rightIntermediate.expr + ").indexOf(" + leftIntermediate.expr + ")";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "ssub")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "(" + rightIntermediate.expr + ").replace(" + leftIntermediate.expr + ", '')";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "symb")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "(" + leftIntermediate.expr + ")[" + rightIntermediate.expr + "]";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "scmp")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + " !== " + rightIntermediate.expr;
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else if (token.ToLower() == "scmi")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = "(" + leftIntermediate.expr + ").toLowerCase() !== (" + rightIntermediate.expr + ").toLowerCase()";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else
                        {
                            XmlHelper.writeLog("Failed to apply " + token);
                            return "";
                        }
                    }
                    else
                    {
                        if (token.Length >= 2 && token[0] == '@')
                        {
                            stack.Push(new Intermediate(token.Substring(1), "VAR"));
                        }
                        else if (Regex.IsMatch(token, @"^[-0-9.]+$"))
                        {
                            stack.Push(new Intermediate(token, "NUM"));
                        }
                        else if (Regex.IsMatch(token, @"0[xX][0-9A-Fa-f]+"))
                        {
                            stack.Push(new Intermediate(token, "HEX"));
                        }
                        else if (token.Length >= 2 && token[0] == '\'' && token[token.Length - 1] == '\'')
                        {
                            stack.Push(new Intermediate(token, "STR"));
                        }
                        else if (token.ToLower() == "true" || token.ToLower() == "false") {
                            stack.Push(new Intermediate(token.ToLower(), "SYS"));
                        }
                        else
                        {
                            // OPERATOR
                            switch (token.ToLower())
                            {
                                case "dnor":
                                case "d360":
                                case "rdeg":
                                case "rnor":
                                    // JUST SKIP
                                    break;

                                case "pi":
                                    stack.Push(new Intermediate("Math.PI", ""));
                                    break;
                                case "dgrd":
                                    addExpression(stack, token, "* Math.PI/180", "after", 1, XmlHelper);
                                    break;
                                case "rddg":
                                    addExpression(stack, token, "* 180/Math.PI", "after", 1, XmlHelper);
                                    break;
                                case "abs":
                                    addExpression(stack, token, "Math.abs", "before", 1, XmlHelper);
                                    break;
                                case "int":
                                case "flr":
                                    addExpression(stack, token, "Math.floor", "before", 1, XmlHelper);
                                    break;
                                case "cos":
                                    addExpression(stack, token, "Math.cos", "before", 1, XmlHelper);
                                    break;
                                case "sin":
                                    addExpression(stack, token, "Math.sin", "before", 1, XmlHelper);
                                    break;
                                case "acos":
                                    addExpression(stack, token, "Math.acos", "before", 1, XmlHelper);
                                    break;
                                //case "ctg":
                                //    addExpression(stack, token, "Math.cot", "before", XmlHelper);
                                //    break;
                                case "ln":
                                    addExpression(stack, token, "Math.log", "before", 1, XmlHelper);
                                    break;
                                case "sqr":
                                    addExpression(stack, token, " ** 2", "after", 1, XmlHelper);
                                    break;
                                case "asin":
                                    addExpression(stack, token, "Math.asin", "before", 1, XmlHelper);
                                    break;
                                case "sqrt":
                                    addExpression(stack, token, "Math.sqrt", "before", 1, XmlHelper);
                                    break;
                                case "exp":
                                    addExpression(stack, token, "Math.exp", "before", 1, XmlHelper);
                                    break;
                                case "tg":
                                    addExpression(stack, token, "Math.tan", "before", 1, XmlHelper);
                                    break;
                                case "atg":
                                    addExpression(stack, token, "Math.atan", "before", 1, XmlHelper);
                                    break;
                                case "ceil":
                                    addExpression(stack, token, "Math.ceil", "before", 1, XmlHelper);
                                    break;
                                case "near":
                                    addExpression(stack, token, "Math.round", "before", 1, XmlHelper);
                                    break;
                                case "min":
                                    addExpression(stack, token, "Math.min", "before", 2, XmlHelper);
                                    break;
                                case "max":
                                    addExpression(stack, token, "Math.max", "before", 2, XmlHelper);
                                    break;
                                case "pow":
                                    addExpression(stack, token, "Math.pow", "before", 2, XmlHelper);
                                    break;
                                case "~":
                                    addExpression(stack, token, "~", "before", 1, XmlHelper);
                                    break;
                                case "eps":
                                    addExpression(stack, token, "Number.EPSILON * ", "before", 1, XmlHelper);
                                    break;
                                case "atg2":
                                    addExpression(stack, token, "Math.atan2", "before", 2, XmlHelper);
                                    break;

                                case "lc":
                                    addExpression(stack, token, ".toLowerCase()", "after", 1, XmlHelper);
                                    break;
                                case "uc":
                                case "cap":
                                    addExpression(stack, token, ".toUpperCase()", "after", 1, XmlHelper);
                                    break;
                                case "chr":
                                    addExpression(stack, token, "String.fromCharCode", "before", 1, XmlHelper);
                                    break;
                                case "ord":
                                    addExpression(stack, token, ".charCodeAt(0)", "after", 1, XmlHelper);
                                    break;

                                default:
                                    XmlHelper.writeLog("Unknown operator \"" + token + "\"");
                                    return "";

                            }

                        }
                    }

                    backupStack = stack;

                    string stackLog = "";
                    foreach (var stackItem in stack)
                        stackLog = stackItem.expr + " | " + stackLog;

                    string condLog = "";
                    foreach (var condItem in ifCond)
                        condLog = condItem.expr + " | " + condLog;



                    XmlHelper.writeLog("Main stack: " + stackLog);
                    XmlHelper.writeLog("Condition stack: " + condLog);
                    XmlHelper.writeLog("");

                }


                int i = 0;
                foreach (var obj in stack)
                {
                    XmlHelper.writeLog("Final stack #"+i+": " + obj.expr);
                    i++;
                }

                if (stack.Count > 0)
                    return stack.Peek().expr;
                else
                    return "";
            }
            catch (Exception ex)
            {
                XmlHelper.writeLog(ex.ToString());
                return "";
            }
        }

        private void addExpression(Stack<Intermediate> stack, string token, string modifier, string position, int arguments, xmlHelper XmlHelper)
        {
            string newExpr = "";

            if (arguments == 2 && stack.Count >= 2)
            {
                var rightIntermediate = stack.Pop();
                var leftIntermediate = stack.Pop();
                newExpr = (position == "before" ? modifier : "") + "( " + leftIntermediate.expr + ", " + rightIntermediate.expr + " )" + (position == "after" ? modifier : "");
            }
            else if (arguments == 1 && stack.Count >= 1)
            {
                var rightIntermediate = stack.Pop();
                newExpr = (position == "before" ? modifier : "") + "(" + rightIntermediate.expr + ")" + (position == "after" ? modifier : "");
            }
            else if (arguments == 0)
            {
                newExpr = (position == "before" ? modifier + "()" : "") + (position == "after" ? "1" + modifier : "");
            } else
                XmlHelper.writeLog("Failed to apply " + token);

            stack.Push(new Intermediate(newExpr, token));
        }
        // POSTFIX CONVERTER ENDS

    }
}
