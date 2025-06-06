﻿using System;
using System.Collections.Generic;
using System.Text;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

using IgorKL.ACAD3.Model.Helpers.SdrFormat;
using wnd = System.Windows.Forms;

using IgorKL.ACAD3.Model.Extensions;

namespace IgorKL.ACAD3.Model.Commands {
    public partial class PointsCmd {
        [RibbonCommandButton("Импорт точек Sdr", RibbonPanelCategories.Points_Coordinates)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_ImportSdrData", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public void ImportSdrData() {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
            SdrReader rd = new SdrReader();
            wnd.OpenFileDialog opfWndDia = new wnd.OpenFileDialog {
                AddExtension = true,
                Filter = "Text files (*.txt)|*.txt|Sdr files (*.sdr)|*.sdr|All files (*.*)|*.*",
                FilterIndex = 2
            };
            if (opfWndDia.ShowDialog() == wnd.DialogResult.OK) {
                PromptIntegerOptions scaleOpt = new PromptIntegerOptions("\nУкажите маштаб, 1:") {
                    UseDefaultValue = true,
                    DefaultValue = 1000,
                    AllowNegative = false,
                    AllowZero = false,
                    AllowNone = false
                };
                PromptIntegerResult scaleRes = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.GetInteger(scaleOpt);
                if (scaleRes.Status != PromptStatus.OK)
                    return;
                double scale = scaleRes.Value / 1000d;

                PromptIntegerOptions digCountOpt = new PromptIntegerOptions("\nЗнаков после запятой") {
                    UseDefaultValue = true,
                    DefaultValue = 2,
                    AllowNegative = false
                };
                PromptIntegerResult digCountRes = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.GetInteger(digCountOpt);
                if (digCountRes.Status != PromptStatus.OK)
                    return;

                string path = opfWndDia.FileName;
                var points = rd._SdrCoordParser(path);
                if (points == null)
                    return;
                string lname = "__SDRP_" + System.IO.Path.GetFileNameWithoutExtension(opfWndDia.SafeFileName);
                string lnameElev = lname + "__Elevations";
                string lnameName = lname + "__Names";
                Layers.LayerTools.CreateHiddenLayer(lname);
                Layers.LayerTools.CreateHiddenLayer(lnameElev);
                Layers.LayerTools.CreateHiddenLayer(lnameName);

                using (Transaction trans = Tools.StartTransaction()) {
                    BlockTable acBlkTbl = trans.GetObject(Application.DocumentManager.MdiActiveDocument.Database.BlockTableId,
                                                 OpenMode.ForRead) as BlockTable;

                    BlockTableRecord acBlkTblRec = trans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                    OpenMode.ForWrite) as BlockTableRecord;

                    foreach (var p in points) {

                        DBPoint acPoint = new DBPoint(new Point3d(p.y, p.x, p.h)) {
                            Layer = lname
                        };
                        acPoint.SetDatabaseDefaults();
                        Group gr = new Group();

                        string format = digCountRes.Value == 0 ? "#0" : ((Func<string>)(() => { format = "#0."; for (int i = 0; i < digCountRes.Value; i++) format += "0"; return format; })).Invoke();

                        var text = _CreateText(new Point3d(p.y + 2.0 * scale, p.x, 0), Math.Round(p.h, digCountRes.Value, MidpointRounding.AwayFromZero).ToString(format, culture), lnameElev, scale);

                        var nameText = _CreateText(text, p.name, lnameName, scale);

                        acBlkTblRec.AppendEntity(acPoint);
                        trans.AddNewlyCreatedDBObject(acPoint, true);

                        ObjectId elevId = acBlkTblRec.AppendEntity(text);
                        trans.AddNewlyCreatedDBObject(text, true);


                        ObjectId nameId = acBlkTblRec.AppendEntity(nameText);
                        trans.AddNewlyCreatedDBObject(nameText, true);

                        gr.Append(elevId);
                        gr.Append(nameId);
                        Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database.AddDBObject(gr);

                        trans.AddNewlyCreatedDBObject(gr, true);

                    }

                    trans.Commit();
                }

            }
        }

        [RibbonCommandButton("Импорт точек Cogo из Sdr", RibbonPanelCategories.Points_Coordinates, true)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_ImportSdrDataCivil", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public void ImportSdrDataCivil() {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
            SdrReader rd = new SdrReader();
            wnd.OpenFileDialog opfWndDia = new wnd.OpenFileDialog {
                AddExtension = true,
                Filter = "Text files (*.txt)|*.txt|Sdr files (*.sdr)|*.sdr|All files (*.*)|*.*",
                FilterIndex = 2
            };

            if (opfWndDia.ShowDialog() == wnd.DialogResult.OK) {
                string path = opfWndDia.FileName;
                var points = rd._SdrCoordParser(path);
                if (points == null)
                    return;
                foreach (var p in points) {
                    IgorKL.ACAD3.Model.CogoPoints.CogoPointFactory.CreateCogoPoints(new Point3d(p.y, p.x, p.h), p.name, p.code2);
                }
            }
        }

        [RibbonCommandButton("Экспорт точек в Sdr", RibbonPanelCategories.Points_Coordinates)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_ExportSdrData", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public void ExportSdrData() {

            if (!TrySelectObjects<DBPoint>(out IList<DBPoint> points, OpenMode.ForRead, "\nВыберите точки"))
                return;
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            System.Windows.Forms.SaveFileDialog saveFileDialog1 = new wnd.SaveFileDialog();
            string pattern = "#0.000";
            saveFileDialog1.DefaultExt = "sdr";
            saveFileDialog1.AddExtension = true;
            System.Windows.Forms.DialogResult dres = saveFileDialog1.ShowDialog();

            if (dres == System.Windows.Forms.DialogResult.OK || dres == System.Windows.Forms.DialogResult.Yes) {
                string path = saveFileDialog1.FileName;

                StringBuilder sb = new StringBuilder();
                int i = 0;
                foreach (DBPoint p in points) {
                    var line = SdrWriter.SdrFormatter.CreateSdrLine(false);
                    line.Fields[0].Value = "08KI";
                    line.Fields[1].Value = "num_" + (++i).ToString("#0", culture);
                    line.Fields[2].Value = p.Position.Y.ToString(pattern, culture);
                    line.Fields[3].Value = p.Position.X.ToString(pattern, culture);
                    line.Fields[4].Value = p.Position.Z.ToString(pattern, culture);
                    line.Fields[5].Value = "A";
                    if (line.Fields.Count >= 7)
                        line.Fields[6].Value = "STN";
                    sb.AppendLine(line.ToString());
                }
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(path)) {
                    sw.Write(sb);
                }
            }
        }

        [RibbonCommandButton("Точки из буфера", RibbonPanelCategories.Points_Coordinates)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_CreateAcadPointsFromBuffer")]
        public void CreateAcadPointsFromBuffer() {
            var editor = Tools.GetAcadEditor();

            PromptKeywordOptions kwOpt = new PromptKeywordOptions("\nSelect") {
                AllowNone = false
            };
            kwOpt.Keywords.Add("FromText");
            kwOpt.Keywords.Add("FromAcadText");

            PromptResult kwRes = editor.GetKeywords(kwOpt);

            if (kwRes.Status != PromptStatus.OK)
                return;

            PromptIntegerOptions scaleOpt = new PromptIntegerOptions("\nSpecify the scale, 1: ") {
                UseDefaultValue = true,
                DefaultValue = 1000,
                AllowNegative = false,
                AllowZero = false,
                AllowNone = false
            };
            PromptIntegerResult scaleRes = editor.GetInteger(scaleOpt);
            if (scaleRes.Status != PromptStatus.OK)
                return;
            double scale = scaleRes.Value / 1000d;

            PromptIntegerOptions digCountOpt = new PromptIntegerOptions("\nNumber of decimal places: ") {
                UseDefaultValue = true,
                DefaultValue = 2,
                AllowNegative = false
            };
            PromptIntegerResult digCountRes = editor.GetInteger(digCountOpt);
            if (digCountRes.Status != PromptStatus.OK)
                return;

            PromptKeywordOptions groupingOpt = new PromptKeywordOptions("\nGroup data? : ") {
                AllowNone = false
            };
            groupingOpt.Keywords.Add("Yes");
            groupingOpt.Keywords.Add("No");
            groupingOpt.Keywords.Default = "Yes";
            PromptResult groupingRes = editor.GetKeywords(groupingOpt);
            if (groupingRes.Status != PromptStatus.OK)
                return;

            string lname = "__CLIPP" + "POINTS";
            string lnameElev = lname + "__Elevations";
            string lnameName = lname + "__Names";
            Layers.LayerTools.CreateHiddenLayer(lname);
            Layers.LayerTools.CreateHiddenLayer(lnameElev);
            Layers.LayerTools.CreateHiddenLayer(lnameName);

            string data;
            switch (kwRes.StringResult) {
                case "FromText": {
                    data = System.Windows.Forms.Clipboard.GetText();
                    break;
                }
                case "FromAcadText": {
                    data = "\t" + _getDbTextString("\nSelect text", "is't DBText");
                    data += "\t" + _getDbTextString("\nSelect text", "is't DBText");
                    break;
                }
                default:
                    return;
            }

            data = data.Replace(',', '.');
            editor.WriteMessage(data);
            string[] lines = data.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            using (Transaction trans = Tools.StartTransaction()) {
                BlockTable acBlkTbl = trans.GetObject(Application.DocumentManager.MdiActiveDocument.Database.BlockTableId,
                                                OpenMode.ForRead) as BlockTable;

                BlockTableRecord acBlkTblRec = trans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                OpenMode.ForWrite) as BlockTableRecord;
                int count = lines.Length;
                foreach (string l in lines) {
                    string[] coords = l.Split(new char[] { '\t' }, StringSplitOptions.None);
                    try {
                        string name = coords[0];
                        double y = double.Parse(coords[1], culture);
                        double x = double.Parse(coords[2], culture);
                        double h = 0d;
                        ObjectId nameId = ObjectId.Null;
                        ObjectId pointId = ObjectId.Null;
                        ObjectId elevId = ObjectId.Null;

                        if (coords.Length > 3) {
                            try {
                                h = double.Parse(coords[3], culture);
                            }
                            catch { }
                            if (coords[3].Length > 0) {
                                string f = "#0";
                                if (digCountRes.Value > 0)
                                    f = (f += ".").PadRight(f.Length + digCountRes.Value, '0');
                                var text = _CreateText(new Point3d(x + 2d * scale, y, 0d), h.ToString(f), lnameElev, scale);
                                elevId = acBlkTblRec.AppendEntity(text);
                                trans.AddNewlyCreatedDBObject(text, true);
                            }
                        }

                        DBPoint point = new DBPoint(new Point3d(x, y, h));
                        point.SetDatabaseDefaults();
                        point.Layer = lname;
                        pointId = acBlkTblRec.AppendEntity(point);
                        trans.AddNewlyCreatedDBObject(point, true);

                        if (name.Length > 0) {
                            var text = _CreateText(new Point3d(x + 2d * scale, y + 3.0d * scale, 0d), name, lnameName, scale);
                            nameId = acBlkTblRec.AppendEntity(text);
                            trans.AddNewlyCreatedDBObject(text, true);
                        }

                        if (groupingRes.StringResult == "Yes") {
                            Group gr = new Group();
                            if (!nameId.IsNull)
                                gr.Append(nameId);
                            if (!elevId.IsNull)
                                gr.Append(elevId);
                            Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database.AddDBObject(gr);
                            trans.AddNewlyCreatedDBObject(gr, true);
                        }
                    }
                    catch { count -= 1; }
                }

                trans.Commit();
            }
        }

        [RibbonCommandButton("Текст в буфер", RibbonPanelCategories.Text_Annotations)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_SetTextDataToBuffer", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public void SetTextDataToBuffer() {
            const string sep = "\t";

            if (!ObjectCollector.TrySelectObjects(out List<DBText> data, "\nВыберете текстовые объекты для импорта в буфер обмена: "))
                return;

            if (data.Count == 0)
                return;

            string res = "";
            using (Transaction trans = Tools.StartTransaction()) {
                foreach (var text in data) {
                    res += text.TextString + sep;
                }
            }

            res = res.Remove(res.Length - sep.Length);
            System.Windows.Forms.Clipboard.SetText(res);
        }

        [RibbonCommandButton("Круги в точки", RibbonPanelCategories.Points_Coordinates)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_ConvertCircleToPoint", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public void iCmd_ConvertCircleToPoint() {
            if (!TrySelectObjects<Circle>(out IList<Circle> circles, OpenMode.ForRead, "\nВыберите круги: "))
                return;
            try {

                using (Transaction trans = Tools.StartTransaction()) {
                    BlockTable acBlkTbl;
                    acBlkTbl = trans.GetObject(Application.DocumentManager.MdiActiveDocument.Database.BlockTableId,
                                                    OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkTblRec;
                    acBlkTblRec = trans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                    OpenMode.ForWrite) as BlockTableRecord;
                    foreach (var circle in circles) {
                        DBPoint point = new DBPoint(new Point3d(circle.Center.ToArray()));
                        point.SetDatabaseDefaults();
                        acBlkTblRec.AppendEntity(point);
                        trans.AddNewlyCreatedDBObject(point, true);

                    }
                    trans.Commit();
                }
            }
            catch (Exception ex) { Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(ex.Message); }
        }

        [RibbonCommandButton("Cogo точки из буфера", RibbonPanelCategories.Points_Coordinates, true)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_CreateCogoPointsFromBuffer")]
        public void CreateCogoPointsFromBuffer() {
            CogoPoints.Views.PointStylesSelectorControl control = null;
            MainMenu.MainPaletteSet ps = null;

            if (MainMenu.MainPaletteSet.CreatedInstance.FindVisual("Cogo точки из буфера") == null) {
                control = new CogoPoints.Views.PointStylesSelectorControl();
                ps = MainMenu.MainPaletteSet.CreatedInstance;
                control = (CogoPoints.Views.PointStylesSelectorControl)ps.AddControl("Cogo точки из буфера", control);
                ps.Show();
                control.CommandAction = CreateCogoPointsFromBuffer;
            }
            else
            {
                ps = Model.MainMenu.MainPaletteSet.CreatedInstance;
                control = ps.FindVisual("Cogo точки из буфера") as IgorKL.ACAD3.Model.CogoPoints.Views.PointStylesSelectorControl;
                if (control == null)
                    return;
                if (!ps.Visible) {
                    ps.Show();
                    return;
                }

                string separator = "\t";
                string data = System.Windows.Forms.Clipboard.GetText();
                data = data.Replace(',', '.');
                string[] lines = data.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);

                using (Transaction trans = HostApplicationServices.WorkingDatabase.TransactionManager.StartTransaction()) {

                    foreach (string line in lines) {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        string[] fields = line.Split(new[] { separator }, StringSplitOptions.None);
                        if (fields.Length < 3)
                            continue;

                        try {
                            string name = fields[0];
                            double north = double.Parse(fields[1], System.Globalization.NumberStyles.Number, Tools.Culture);
                            double east = double.Parse(fields[2], System.Globalization.NumberStyles.Number, Tools.Culture);
                            double elevation = 0;
                            if (fields.Length > 3)
                                elevation = double.Parse(fields[3], System.Globalization.NumberStyles.Number, Tools.Culture);
                            string description = "_PointsFromBuffer";
                            if (fields.Length > 4)
                                description = fields[4];

                            ObjectId pointId = IgorKL.ACAD3.Model.CogoPoints.CogoPointFactory.CreateCogoPoints(new Point3d(east, north, elevation), name, description);
                            var point = trans.GetObject(pointId, OpenMode.ForWrite) as CogoPoint;

                            point.SetDatabaseDefaults();

                            point.LabelStyleId = control.SelectedPointLabelStyle.Key;
                            point.StyleId = control.SelectedPointStyle.Key;
                        }
                        catch (System.Exception ex) {
                            Tools.GetAcadEditor().WriteMessage(string.Format("\nCreate point error, message: {0}", ex.Message));
                        }
                    }
                    HostApplicationServices.WorkingDatabase.TransactionManager.QueueForGraphicsFlush();
                    trans.Commit();
                }
            }
        }

        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_EditPointElevation", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public void EditPointElevation() {
            if (!ObjectCollector.TrySelectObjects(out List<CogoPoint> points, "\nSelect points"))
                return;

            PromptDoubleOptions valueOption = new PromptDoubleOptions("\nEnter value") {
                AllowNone = false,
                DefaultValue = 0d
            };

            PromptDoubleResult valueResult = Tools.GetAcadEditor().GetDouble(valueOption);
            if (valueResult.Status != PromptStatus.OK)
                return;

            Model.CogoPoints.CogoPointEditor.EditElevation(points, valueResult.Value);
        }

        [RibbonCommandButton("Случайная отметка", RibbonPanelCategories.Points_Coordinates, true)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_EditPointElevationRandom", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public void EditPointElevationRandom() {
            var keywords = new { PositiveOnly = "PositiveOnly", NegativeOnly = "NegativeOnly", Both = "Both" };

            if (!ObjectCollector.TrySelectObjects(out List<CogoPoint> points, "\nSelect points"))
                return;

            PromptDoubleOptions valueOption = new PromptDoubleOptions("\nEnter value") {
                AllowNone = false,
                DefaultValue = 0d,
                AllowNegative = false
            };

            PromptDoubleResult valueResult = Tools.GetAcadEditor().GetDouble(valueOption);
            if (valueResult.Status != PromptStatus.OK)
                return;

            PromptKeywordOptions options = new PromptKeywordOptions("\nEnter method") {
                AppendKeywordsToMessage = true,
                AllowArbitraryInput = true
            };
            options.Keywords.Add(keywords.PositiveOnly);
            options.Keywords.Add(keywords.NegativeOnly);
            options.Keywords.Add(keywords.Both);
            options.AppendKeywordsToMessage = true;
            options.AllowNone = true;
            options.Keywords.Default = keywords.Both;

            PromptResult keywordResult = Tools.GetAcadEditor().GetKeywords(options);
            if (keywordResult.Status != PromptStatus.OK) {
                return;
            }

            double ratio = keywordResult.StringResult == keywords.Both ? -0.5 : 0d;
            double sign = keywordResult.StringResult == keywords.NegativeOnly ? -1d : 1d;
            if (keywordResult.StringResult == keywords.Both)
                sign = 2d;

            Random random = new Random(DateTime.Now.Second);

            foreach (CogoPoint point in points)
                point.Elevation += (random.NextDouble() + ratio) * valueResult.Value * sign;
        }

        [RibbonCommandButton("Описание в Имя Cogo", RibbonPanelCategories.Points_Coordinates, true)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_ReplacePointDescriptionToName", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public void ReplacePointDescriptionToName() {
            if (!ObjectCollector.TrySelectObjects(out List<CogoPoint> points, "\nSelect points"))
                return;
            foreach (CogoPoint point in points)
                point.PointName = point.FullDescription;
        }

        [RibbonCommandButton("Точки из блков", RibbonPanelCategories.Points_Coordinates)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_ConvertBlocToPoint", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public static void ConvertBlocToPoint() {
            if (!TrySelectObjects(out IList<BlockReference> blocks, OpenMode.ForRead, "\nВыберите блоки"))
                return;

            using (Transaction trans = Tools.StartTransaction()) {
                BlockTable acBlkTbl;
                acBlkTbl = trans.GetObject(Application.DocumentManager.MdiActiveDocument.Database.BlockTableId,
                                                OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkTblRec;
                acBlkTblRec = trans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                OpenMode.ForWrite) as BlockTableRecord;

                foreach (BlockReference block in blocks) {
                    BlockReference db_block = trans.GetObject(block.Id, OpenMode.ForRead) as BlockReference;
                    DBPoint point = new DBPoint(new Point3d(db_block.Position.ToArray()));
                    point.SetDatabaseDefaults();
                    acBlkTblRec.AppendEntity(point);
                    trans.AddNewlyCreatedDBObject(point, true);
                }

                trans.Commit();

            }
        }

        [RibbonCommandButton("Точки Cogo из блоков", RibbonPanelCategories.Points_Coordinates, true)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_ConvertBlocToCogoPoint", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public static void ConvertBlocToCogoPoint() {
            if (!TrySelectObjects(out IList<BlockReference> blocks, OpenMode.ForRead, "\nВыберите блоки")) {
                return;
            }

            var stringOption = new PromptStringOptions("\nВведите исходное описание точек") {
                DefaultValue = "",
                UseDefaultValue = true,
                AllowSpaces = true
            };

            var stringResult = Tools.GetAcadEditor().GetString(stringOption);
            if (stringResult.Status != PromptStatus.OK) {
                return;
            }

            using (Transaction trans = Tools.StartTransaction()) {

                foreach (BlockReference block in blocks) {
                    BlockReference db_block = trans.GetObject(block.Id, OpenMode.ForRead) as BlockReference;
                    CogoPoints.CogoPointFactory.CreateCogoPoints(db_block.Position, trans, description: stringResult.Status == PromptStatus.OK ? stringResult.StringResult : null);
                }

                trans.Commit();
            }
        }

        [RibbonCommandButton("Замена блоков", RibbonPanelCategories.Points_Coordinates)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_BlocChange", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public static void BlocChange() {
            if (!TrySelectObjects(out IList<BlockReference> blocksIn, OpenMode.ForRead, "\nВыберите исходные блоки")) {
                return;
            }

            if (!ObjectCollector.TrySelectAllowedClassObject(out BlockReference blockOut, "\nВыберите блок для замены")) {
                return;
            }

            using (Transaction trans = Tools.StartTransaction()) {
                BlockTable acBlkTbl;
                acBlkTbl = trans.GetObject(Application.DocumentManager.MdiActiveDocument.Database.BlockTableId,
                                                OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkTblRec;
                acBlkTblRec = trans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                OpenMode.ForWrite) as BlockTableRecord;

                BlockReference db_block_src = trans.GetObject(blockOut.Id, OpenMode.ForRead) as BlockReference;
                foreach (BlockReference block in blocksIn) {
                    BlockReference db_block = trans.GetObject(block.Id, OpenMode.ForWrite) as BlockReference;

                    var newBlock = db_block_src.Clone() as BlockReference;
                    newBlock.Position = db_block.Position;
                    newBlock.SetDatabaseDefaults();
                    acBlkTblRec.AppendEntity(newBlock);
                    trans.AddNewlyCreatedDBObject(newBlock, true);
                    db_block.Erase();
                }

                trans.Commit();
            }
        }


        [RibbonCommandButton("Метки поверхности из точек", RibbonPanelCategories.Points_Coordinates, true)]
        [Autodesk.AutoCAD.Runtime.CommandMethod("iCmd_SurfaceElevationLabelsFromPoints", Autodesk.AutoCAD.Runtime.CommandFlags.UsePickSet)]
        public static void CreateSurfaceElevationLabels() {
            if (!ObjectCollector.TrySelectObjects(out List<DBPoint> points, "\nВыберите точки")) {
                return;
            }

            if (!ObjectCollector.TrySelectAllowedClassObject(out CivilSurface surface, "\nУкажите поверхность"))
                return;

            TinSurface tinSurface = surface as TinSurface;
            if (tinSurface == null) {
                Tools.Write("Ошибка. Поверхность должна быть типа: TinSurface");
                return;
            }

            Tools.UseTransaction((trans, _, __) => {
                var civilDoc = Tools.GetActiveCivilDocument();
                var surfaceDb = trans.GetObject(surface.ObjectId, OpenMode.ForWrite);
                points.ForEach(p => {
                    var pointDb = trans.GetObject(p.ObjectId, OpenMode.ForRead) as DBPoint;
                    try {
                        tinSurface.FindElevationAtXY(pointDb.Position.X, pointDb.Position.Y);
                    } catch (Autodesk.Civil.PointNotOnEntityException) {
                        return;
                    } catch {
                        Tools.Write("Error on get point elevation [CreateSurfaceElevationLabels]");
                        return;
                    }

                    var labelId = SurfaceElevationLabel.Create(surfaceDb.ObjectId, pointDb.Position.Convert2d());
                    System.Diagnostics.Debug.Assert(labelId.IsValid && !labelId.IsNull);
                });

                trans.Commit();
            });
        }
    }
}
