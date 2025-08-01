using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.util;
/*
 * Copyright 2003-2005 by Paulo Soares.
 *
 * The contents of this file are subject to the Mozilla Public License Version 1.1
 * (the "License"); you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.mozilla.org/MPL/
 *
 * Software distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
 * for the specific language governing rights and limitations under the License.
 *
 * The Original Code is 'iText, a free JAVA-PDF library'.
 *
 * The Initial Developer of the Original Code is Bruno Lowagie. Portions created by
 * the Initial Developer are Copyright (C) 1999, 2000, 2001, 2002 by Bruno Lowagie.
 * All Rights Reserved.
 * Co-Developer of the code is Paulo Soares. Portions created by the Co-Developer
 * are Copyright (C) 2000, 2001, 2002 by Paulo Soares. All Rights Reserved.
 *
 * Contributor(s): all the names of the contributors are added in the source code
 * where applicable.
 *
 * Alternatively, the contents of this file may be used under the terms of the
 * LGPL license (the "GNU LIBRARY GENERAL PUBLIC LICENSE"), in which case the
 * provisions of LGPL are applicable instead of those above.  If you wish to
 * allow use of your version of this file only under the terms of the LGPL
 * License and not to allow others to use your version of this file under
 * the MPL, indicate your decision by deleting the provisions above and
 * replace them with the notice and other provisions required by the LGPL.
 * If you do not delete the provisions above, a recipient may use your version
 * of this file under either the MPL or the GNU LIBRARY GENERAL PUBLIC LICENSE.
 *
 * This library is free software; you can redistribute it and/or modify it
 * under the terms of the MPL as stated above or under the terms of the GNU
 * Library General Public License as published by the Free Software Foundation;
 * either version 2 of the License, or any later version.
 *
 * This library is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU Library general Public License for more
 * details.
 *
 * If you didn't download this code from the following link, you should check if
 * you aren't using an obsolete version:
 * http://www.lowagie.com/iText/
 */

namespace iTextSharp.text.pdf {
    /** Query and change fields in existing documents either by method
    * calls or by FDF merging.
    * @author Paulo Soares (psoares@consiste.pt)
    */
    public class AcroFields {

        internal PdfReader reader;
        internal PdfWriter writer;
        internal Hashtable fields;
        private int topFirst;
        private Hashtable sigNames;
        private bool append;
        public const int DA_FONT = 0;
        public const int DA_SIZE = 1;
        public const int DA_COLOR = 2;
        private Hashtable extensionFonts = new Hashtable();
        private XfaForm xfa;
        /**
        * A field type invalid or not found.
        */    
        public const int FIELD_TYPE_NONE = 0;
        /**
        * A field type.
        */    
        public const int FIELD_TYPE_PUSHBUTTON = 1;
        /**
        * A field type.
        */    
        public const int FIELD_TYPE_CHECKBOX = 2;
        /**
        * A field type.
        */    
        public const int FIELD_TYPE_RADIOBUTTON = 3;
        /**
        * A field type.
        */    
        public const int FIELD_TYPE_TEXT = 4;
        /**
        * A field type.
        */    
        public const int FIELD_TYPE_LIST = 5;
        /**
        * A field type.
        */    
        public const int FIELD_TYPE_COMBO = 6;
        /**
        * A field type.
        */    
        public const int FIELD_TYPE_SIGNATURE = 7;
        
        private bool lastWasString;
        
        /** Holds value of property generateAppearances. */
        private bool generateAppearances = true;
        
        private Hashtable localFonts = new Hashtable();
        
        private float extraMarginLeft;
        private float extraMarginTop;
        private ArrayList substitutionFonts;

        internal AcroFields(PdfReader reader, PdfWriter writer) {
            this.reader = reader;
            this.writer = writer;
            xfa = new XfaForm(reader);
            if (writer is PdfStamperImp) {
                append = ((PdfStamperImp)writer).append;
            }
            Fill();
        }

        internal void Fill() {
            fields = new Hashtable();
            PdfDictionary top = (PdfDictionary)PdfReader.GetPdfObjectRelease(reader.Catalog.Get(PdfName.ACROFORM));
            if (top == null)
                return;
            PdfArray arrfds = (PdfArray)PdfReader.GetPdfObjectRelease(top.Get(PdfName.FIELDS));
            if (arrfds == null || arrfds.Size == 0)
                return;
            for (int k = 1; k <= reader.NumberOfPages; ++k) {
                PdfDictionary page = reader.GetPageNRelease(k);
                PdfObject pdfObject = PdfReader.GetPdfObjectRelease(page.Get(PdfName.ANNOTS), page);
                if (!(pdfObject is PdfArray annots))
                    continue;
                for (int j = 0; j < annots.Size; ++j) {
                    PdfDictionary annot = annots.GetAsDict(j);
                    if (annot == null) {
                        PdfReader.ReleaseLastXrefPartial(annots.GetAsIndirectObject(j));
                        continue;
                    }
                    if (!PdfName.WIDGET.Equals(annot.GetAsName(PdfName.SUBTYPE))) {
                        PdfReader.ReleaseLastXrefPartial(annots.GetAsIndirectObject(j));
                        continue;
                    }
                    PdfDictionary widget = annot;
                    PdfDictionary dic = new PdfDictionary();
                    dic.Merge(annot);
                    String name = "";
                    PdfDictionary value = null;
                    PdfObject lastV = null;
                    while (annot != null) {
                        dic.MergeDifferent(annot);
                        PdfString t = annot.GetAsString(PdfName.T);
                        if (t != null)
                            name = t.ToUnicodeString() + "." + name;
                        if (lastV == null && annot.Get(PdfName.V) != null)
                            lastV = PdfReader.GetPdfObjectRelease(annot.Get(PdfName.V));
                        if (value == null &&  t != null) {
                            value = annot;
                            if (annot.Get(PdfName.V) == null && lastV  != null)
                                value.Put(PdfName.V, lastV);
                        }
                        annot = annot.GetAsDict(PdfName.PARENT);
                    }
                    if (name.Length > 0)
                        name = name.Substring(0, name.Length - 1);
                    Item item = (Item)fields[name];
                    if (item == null) {
                        item = new Item();
                        fields[name] = item;
                    }
                    if (value == null)
                        item.AddValue(widget);
                    else
                        item.AddValue(value);
                    item.AddWidget(widget);
                    item.AddWidgetRef(annots.GetAsIndirectObject(j)); // must be a reference
                    if (top != null)
                        dic.MergeDifferent(top);
                    item.AddMerged(dic);
                    item.AddPage(k);
                    item.AddTabOrder(j);
                }
            }
            // some tools produce invisible signatures without an entry in the page annotation array
            // look for a single level annotation
            PdfNumber sigFlags = top.GetAsNumber(PdfName.SIGFLAGS);
            if (sigFlags == null || (sigFlags.IntValue & 1) != 1)
                return;
            for (int j = 0; j < arrfds.Size; ++j) {
                PdfDictionary annot = arrfds.GetAsDict(j);
                if (annot == null) {
                    PdfReader.ReleaseLastXrefPartial(arrfds.GetAsIndirectObject(j));
                    continue;
                }
                if (!PdfName.WIDGET.Equals(annot.GetAsName(PdfName.SUBTYPE))) {
                    PdfReader.ReleaseLastXrefPartial(arrfds.GetAsIndirectObject(j));
                    continue;
                }
                PdfArray kids = (PdfArray)PdfReader.GetPdfObjectRelease(annot.Get(PdfName.KIDS));
                if (kids != null)
                    continue;
                PdfDictionary dic = new PdfDictionary();
                dic.Merge(annot);
                PdfString t = annot.GetAsString(PdfName.T);
                if (t == null)
                    continue;
                String name = t.ToUnicodeString();
                if (fields.ContainsKey(name))
                    continue;
                Item item = new Item();
                fields[name] = item;
                item.AddValue(dic);
                item.AddWidget(dic);
                item.AddWidgetRef(arrfds.GetAsIndirectObject(j)); // must be a reference
                item.AddMerged(dic);
                item.AddPage(-1);
                item.AddTabOrder(-1);
            }
        }
        
        /** Gets the list of appearance names. Use it to get the names allowed
        * with radio and checkbox fields. If the /Opt key exists the values will
        * also be included. The name 'Off' may also be valid
        * even if not returned in the list.
        * @param fieldName the fully qualified field name
        * @return the list of names or <CODE>null</CODE> if the field does not exist
        */    
        public String[] GetAppearanceStates(String fieldName) {
            Item fd = (Item)fields[fieldName];
            if (fd == null)
                return null;
            Hashtable names = new Hashtable();
            PdfDictionary vals = fd.GetValue(0);
            PdfString stringOpt = vals.GetAsString( PdfName.OPT );
            if (stringOpt != null) {
                names[stringOpt.ToUnicodeString()] = null;
            }
            else {
                PdfArray arrayOpt = vals.GetAsArray(PdfName.OPT);
                if (arrayOpt != null) {
                    for (int k = 0; k < arrayOpt.Size; ++k) {
                        PdfString valStr = arrayOpt.GetAsString( k );
                        if (valStr != null)
                            names[valStr.ToUnicodeString()] = null;
                    }
                }
            }
            for (int k = 0; k < fd.Size; ++k) {
                PdfDictionary dic = fd.GetWidget( k );
                dic = dic.GetAsDict(PdfName.AP);
                if (dic == null)
                    continue;
                dic = dic.GetAsDict(PdfName.N);
                if (dic == null)
                    continue;
            foreach (PdfName pname in dic.Keys) {
                    String name = PdfName.DecodeName(pname.ToString());
                    names[name] = null;
                }
            }
            string[] outs = new string[names.Count];
            names.Keys.CopyTo(outs, 0);
            return outs;
        }
        
        private String[] GetListOption(String fieldName, int idx) {
            Item fd = GetFieldItem(fieldName);
            if (fd == null)
                return null;
            PdfArray ar = fd.GetMerged(0).GetAsArray(PdfName.OPT);
            if (ar == null)
                return null;
            String[] ret = new String[ar.Size];
            for (int k = 0; k < ar.Size; ++k) {
                PdfObject obj = ar.GetDirectObject( k );
                try {
                    if (obj.IsArray()) {
                        obj = ((PdfArray)obj).GetDirectObject(idx);
                    }
                    if (obj.IsString())
                        ret[k] = ((PdfString)obj).ToUnicodeString();
                    else
                        ret[k] = obj.ToString();
                }
                catch {
                    ret[k] = "";
                }
            }
            return ret;
        }
    
        /**
        * Gets the list of export option values from fields of type list or combo.
        * If the field doesn't exist or the field type is not list or combo it will return
        * <CODE>null</CODE>.
        * @param fieldName the field name
        * @return the list of export option values from fields of type list or combo
        */    
        public String[] GetListOptionExport(String fieldName) {
            return GetListOption(fieldName, 0);
        }
        
        /**
        * Gets the list of display option values from fields of type list or combo.
        * If the field doesn't exist or the field type is not list or combo it will return
        * <CODE>null</CODE>.
        * @param fieldName the field name
        * @return the list of export option values from fields of type list or combo
        */    
        public String[] GetListOptionDisplay(String fieldName) {
            return GetListOption(fieldName, 1);
        }
        
        /**
        * Sets the option list for fields of type list or combo. One of <CODE>exportValues</CODE>
        * or <CODE>displayValues</CODE> may be <CODE>null</CODE> but not both. This method will only
        * set the list but will not set the value or appearance. For that, calling <CODE>setField()</CODE>
        * is required.
        * <p>
        * An example:
        * <p>
        * <PRE>
        * PdfReader pdf = new PdfReader("input.pdf");
        * PdfStamper stp = new PdfStamper(pdf, new FileOutputStream("output.pdf"));
        * AcroFields af = stp.GetAcroFields();
        * af.SetListOption("ComboBox", new String[]{"a", "b", "c"}, new String[]{"first", "second", "third"});
        * af.SetField("ComboBox", "b");
        * stp.Close();
        * </PRE>
        * @param fieldName the field name
        * @param exportValues the export values
        * @param displayValues the display values
        * @return <CODE>true</CODE> if the operation succeeded, <CODE>false</CODE> otherwise
        */    
        public bool SetListOption(String fieldName, String[] exportValues, String[] displayValues) {
            if (exportValues == null && displayValues == null)
                return false;
            if (exportValues != null && displayValues != null && exportValues.Length != displayValues.Length)
                throw new ArgumentException("The export and the display array must have the same size.");
            int ftype = GetFieldType(fieldName);
            if (ftype != FIELD_TYPE_COMBO && ftype != FIELD_TYPE_LIST)
                return false;
            Item fd = (Item)fields[fieldName];
            String[] sing = null;
            if (exportValues == null && displayValues != null)
                sing = displayValues;
            else if (exportValues != null && displayValues == null)
                sing = exportValues;
            PdfArray opt = new PdfArray();
            if (sing != null) {
                for (int k = 0; k < sing.Length; ++k)
                    opt.Add(new PdfString(sing[k], PdfObject.TEXT_UNICODE));
            }
            else {
                for (int k = 0; k < exportValues.Length; ++k) {
                    PdfArray a = new PdfArray();
                    a.Add(new PdfString(exportValues[k], PdfObject.TEXT_UNICODE));
                    a.Add(new PdfString(displayValues[k], PdfObject.TEXT_UNICODE));
                    opt.Add(a);
                }
            }
            fd.WriteToAll( PdfName.OPT, opt, Item.WRITE_VALUE | Item.WRITE_MERGED );
            return true;
        }
        
        /**
        * Gets the field type. The type can be one of: <CODE>FIELD_TYPE_PUSHBUTTON</CODE>,
        * <CODE>FIELD_TYPE_CHECKBOX</CODE>, <CODE>FIELD_TYPE_RADIOBUTTON</CODE>,
        * <CODE>FIELD_TYPE_TEXT</CODE>, <CODE>FIELD_TYPE_LIST</CODE>,
        * <CODE>FIELD_TYPE_COMBO</CODE> or <CODE>FIELD_TYPE_SIGNATURE</CODE>.
        * <p>
        * If the field does not exist or is invalid it returns
        * <CODE>FIELD_TYPE_NONE</CODE>.
        * @param fieldName the field name
        * @return the field type
        */    
        public int GetFieldType(String fieldName) {
            Item fd = GetFieldItem(fieldName);
            if (fd == null)
                return FIELD_TYPE_NONE;
            PdfDictionary merged = fd.GetMerged( 0 );
            PdfName type = merged.GetAsName(PdfName.FT);
            if (type == null)
                return FIELD_TYPE_NONE;
            int ff = 0;
            PdfNumber ffo = merged.GetAsNumber(PdfName.FF);
            if (ffo != null) {
                ff = ffo.IntValue;
            }
            if (PdfName.BTN.Equals(type)) {
                if ((ff & PdfFormField.FF_PUSHBUTTON) != 0)
                    return FIELD_TYPE_PUSHBUTTON;
                if ((ff & PdfFormField.FF_RADIO) != 0)
                    return FIELD_TYPE_RADIOBUTTON;
                else
                    return FIELD_TYPE_CHECKBOX;
            }
            else if (PdfName.TX.Equals(type)) {
                return FIELD_TYPE_TEXT;
            }
            else if (PdfName.CH.Equals(type)) {
                if ((ff & PdfFormField.FF_COMBO) != 0)
                    return FIELD_TYPE_COMBO;
                else
                    return FIELD_TYPE_LIST;
            }
            else if (PdfName.SIG.Equals(type)) {
                return FIELD_TYPE_SIGNATURE;
            }
            return FIELD_TYPE_NONE;
        }
        
        /**
        * Export the fields as a FDF.
        * @param writer the FDF writer
        */    
        public void ExportAsFdf(FdfWriter writer) {
            foreach (DictionaryEntry entry in fields) {
                Item item = (Item)entry.Value;
                string name = (String)entry.Key;
                PdfObject v = item.GetMerged(0).Get(PdfName.V);
                if (v == null)
                    continue;
                string value = GetField(name);
                if (lastWasString)
                    writer.SetFieldAsString(name, value);
                else
                    writer.SetFieldAsName(name, value);
            }
        }
        
        /**
        * Renames a field. Only the last part of the name can be renamed. For example,
        * if the original field is "ab.cd.ef" only the "ef" part can be renamed.
        * @param oldName the old field name
        * @param newName the new field name
        * @return <CODE>true</CODE> if the renaming was successful, <CODE>false</CODE>
        * otherwise
        */    
        public bool RenameField(String oldName, String newName) {
            int idx1 = oldName.LastIndexOf('.') + 1;
            int idx2 = newName.LastIndexOf('.') + 1;
            if (idx1 != idx2)
                return false;
            if (!oldName.Substring(0, idx1).Equals(newName.Substring(0, idx2)))
                return false;
            if (fields.ContainsKey(newName))
                return false;
            Item item = (Item)fields[oldName];
            if (item == null)
                return false;
            newName = newName.Substring(idx2);
            PdfString ss = new PdfString(newName, PdfObject.TEXT_UNICODE);
            item.WriteToAll( PdfName.T, ss, Item.WRITE_VALUE | Item.WRITE_MERGED);
            item.MarkUsed( this, Item.WRITE_VALUE );
            fields.Remove(oldName);
            fields[newName] = item;
            return true;
        }
        
        public static Object[] SplitDAelements(String da) {
            PRTokeniser tk = new PRTokeniser(PdfEncodings.ConvertToBytes(da, null));
            ArrayList stack = new ArrayList();
            Object[] ret = new Object[3];
            while (tk.NextToken()) {
                if (tk.TokenType == PRTokeniser.TK_COMMENT)
                    continue;
                if (tk.TokenType == PRTokeniser.TK_OTHER) {
                    String oper = tk.StringValue;
                    if (oper.Equals("Tf")) {
                        if (stack.Count >= 2) {
                            ret[DA_FONT] = stack[stack.Count - 2];
                            ret[DA_SIZE] = float.Parse((String)stack[stack.Count - 1], System.Globalization.NumberFormatInfo.InvariantInfo);
                        }
                    }
                    else if (oper.Equals("g")) {
                        if (stack.Count >= 1) {
                            float gray = float.Parse((String)stack[stack.Count - 1], System.Globalization.NumberFormatInfo.InvariantInfo);
                            if (gray != 0)
                                ret[DA_COLOR] = new GrayColor(gray);
                        }
                    }
                    else if (oper.Equals("rg")) {
                        if (stack.Count >= 3) {
                            float red = float.Parse((String)stack[stack.Count - 3], System.Globalization.NumberFormatInfo.InvariantInfo);
                            float green = float.Parse((String)stack[stack.Count - 2], System.Globalization.NumberFormatInfo.InvariantInfo);
                            float blue = float.Parse((String)stack[stack.Count - 1], System.Globalization.NumberFormatInfo.InvariantInfo);
                            ret[DA_COLOR] = new Color(red, green, blue);
                        }
                    }
                    else if (oper.Equals("k")) {
                        if (stack.Count >= 4) {
                            float cyan = float.Parse((String)stack[stack.Count - 4], System.Globalization.NumberFormatInfo.InvariantInfo);
                            float magenta = float.Parse((String)stack[stack.Count - 3], System.Globalization.NumberFormatInfo.InvariantInfo);
                            float yellow = float.Parse((String)stack[stack.Count - 2], System.Globalization.NumberFormatInfo.InvariantInfo);
                            float black = float.Parse((String)stack[stack.Count - 1], System.Globalization.NumberFormatInfo.InvariantInfo);
                            ret[DA_COLOR] = new CMYKColor(cyan, magenta, yellow, black);
                        }
                    }
                    stack.Clear();
                }
                else
                    stack.Add(tk.StringValue);
            }
            return ret;
        }
        
        public void DecodeGenericDictionary(PdfDictionary merged, BaseField tx) {
            int flags = 0;
            // the text size and color
            PdfString da = merged.GetAsString(PdfName.DA);
            if (da != null) {
                Object[] dab = SplitDAelements(da.ToUnicodeString());
                if (dab[DA_SIZE] != null)
                    tx.FontSize = (float)dab[DA_SIZE];
                if (dab[DA_COLOR] != null)
                    tx.TextColor = (Color)dab[DA_COLOR];
                if (dab[DA_FONT] != null) {
                    PdfDictionary font = merged.GetAsDict(PdfName.DR);
                    if (font != null) {
                        font = font.GetAsDict(PdfName.FONT);
                        if (font != null) {
                            PdfObject po = font.Get(new PdfName((String)dab[DA_FONT]));
                            if (po != null && po.Type == PdfObject.INDIRECT) {
                                PRIndirectReference por = (PRIndirectReference)po;
                                BaseFont bp = new DocumentFont((PRIndirectReference)po);
                                tx.Font = bp;
                                int porkey = por.Number;
                                BaseFont porf = (BaseFont)extensionFonts[porkey];
                                if (porf == null) {
                                    if (!extensionFonts.ContainsKey(porkey)) {
                                        PdfDictionary fo = (PdfDictionary)PdfReader.GetPdfObject(po);
                                        PdfDictionary fd = fo.GetAsDict(PdfName.FONTDESCRIPTOR);
                                        if (fd != null) {
                                            PRStream prs = (PRStream)PdfReader.GetPdfObject(fd.Get(PdfName.FONTFILE2));
                                            if (prs == null)
                                                prs = (PRStream)PdfReader.GetPdfObject(fd.Get(PdfName.FONTFILE3));
                                            if (prs == null) {
                                                extensionFonts[porkey] = null;
                                            }
                                            else {
                                                try {
                                                    porf = BaseFont.CreateFont("font.ttf", BaseFont.IDENTITY_H, true, false, PdfReader.GetStreamBytes(prs), null);
                                                }
                                                catch {
                                                }
                                                extensionFonts[porkey] = porf;
                                            }
                                        }
                                    }
                                }
                                if (tx is TextField)
                                    ((TextField)tx).ExtensionFont = porf;
                            }
                            else {
                                BaseFont bf = (BaseFont)localFonts[dab[DA_FONT]];
                                if (bf == null) {
                                    String[] fn = (String[])stdFieldFontNames[dab[DA_FONT]];
                                    if (fn != null) {
                                        try {
                                            String enc = "winansi";
                                            if (fn.Length > 1)
                                                enc = fn[1];
                                            bf = BaseFont.CreateFont(fn[0], enc, false);
                                            tx.Font = bf;
                                        }
                                        catch {
                                            // empty
                                        }
                                    }
                                }
                                else
                                    tx.Font = bf;
                            }
                        }
                    }
                }
            }
            //rotation, border and backgound color
            PdfDictionary mk = merged.GetAsDict(PdfName.MK);
            if (mk != null) {
                PdfArray ar = mk.GetAsArray(PdfName.BC);
                Color border = GetMKColor(ar);
                tx.BorderColor = border;
                if (border != null)
                    tx.BorderWidth = 1;
                ar = mk.GetAsArray(PdfName.BG);
                tx.BackgroundColor = GetMKColor(ar);
                PdfNumber rotation = mk.GetAsNumber(PdfName.R);
                if (rotation != null)
                    tx.Rotation = rotation.IntValue;
            }
            //flags
            PdfNumber nfl = merged.GetAsNumber(PdfName.F);
            flags = 0;
            tx.Visibility = BaseField.VISIBLE_BUT_DOES_NOT_PRINT;
            if (nfl != null) {
                flags = nfl.IntValue;
                if ((flags & PdfFormField.FLAGS_PRINT) != 0 && (flags & PdfFormField.FLAGS_HIDDEN) != 0)
                    tx.Visibility = BaseField.HIDDEN;
                else if ((flags & PdfFormField.FLAGS_PRINT) != 0 && (flags & PdfFormField.FLAGS_NOVIEW) != 0)
                    tx.Visibility = BaseField.HIDDEN_BUT_PRINTABLE;
                else if ((flags & PdfFormField.FLAGS_PRINT) != 0)
                    tx.Visibility = BaseField.VISIBLE;
            }
            //multiline
            nfl = merged.GetAsNumber(PdfName.FF);
            flags = 0;
            if (nfl != null)
                flags = nfl.IntValue;
            tx.Options = flags;
            if ((flags & PdfFormField.FF_COMB) != 0) {
                PdfNumber maxLen = merged.GetAsNumber(PdfName.MAXLEN);
                int len = 0;
                if (maxLen != null)
                    len = maxLen.IntValue;
                tx.MaxCharacterLength = len;
            }
            //alignment
            nfl = merged.GetAsNumber(PdfName.Q);
            if (nfl != null) {
                if (nfl.IntValue == PdfFormField.Q_CENTER)
                    tx.Alignment = Element.ALIGN_CENTER;
                else if (nfl.IntValue == PdfFormField.Q_RIGHT)
                    tx.Alignment = Element.ALIGN_RIGHT;
            }
            //border styles
            PdfDictionary bs = merged.GetAsDict(PdfName.BS);
            if (bs != null) {
                PdfNumber w = bs.GetAsNumber(PdfName.W);
                if (w != null)
                    tx.BorderWidth = w.FloatValue;
                PdfName s = bs.GetAsName(PdfName.S);
                if (PdfName.D.Equals(s))
                    tx.BorderStyle = PdfBorderDictionary.STYLE_DASHED;
                else if (PdfName.B.Equals(s))
                    tx.BorderStyle = PdfBorderDictionary.STYLE_BEVELED;
                else if (PdfName.I.Equals(s))
                    tx.BorderStyle = PdfBorderDictionary.STYLE_INSET;
                else if (PdfName.U.Equals(s))
                    tx.BorderStyle = PdfBorderDictionary.STYLE_UNDERLINE;
            }
            else {
                PdfArray bd = merged.GetAsArray(PdfName.BORDER);
                if (bd != null) {
                    if (bd.Size >= 3)
                        tx.BorderWidth = bd.GetAsNumber(2).FloatValue;
                    if (bd.Size >= 4)
                        tx.BorderStyle = PdfBorderDictionary.STYLE_DASHED;
                }
            }
        }

        internal PdfAppearance GetAppearance(PdfDictionary merged, String[] values, String fieldName) {
            topFirst = 0;
            String text = (values.Length > 0) ? values[0] : null;
            TextField tx = null;
            if (fieldCache == null || !fieldCache.ContainsKey(fieldName)) {
                tx = new TextField(writer, null, null);
                tx.SetExtraMargin(extraMarginLeft, extraMarginTop);
                tx.BorderWidth = 0;
                tx.SubstitutionFonts = substitutionFonts;
                DecodeGenericDictionary(merged, tx);
                //rect
                PdfArray rect = merged.GetAsArray(PdfName.RECT);
                Rectangle box = PdfReader.GetNormalizedRectangle(rect);
                if (tx.Rotation == 90 || tx.Rotation == 270)
                    box = box.Rotate();
                tx.Box = box;
                if (fieldCache != null)
                    fieldCache[fieldName] = tx;
            }
            else {
                tx = (TextField)fieldCache[fieldName];
                tx.Writer = writer;
            }
            PdfName fieldType = merged.GetAsName(PdfName.FT);
            if (PdfName.TX.Equals(fieldType)) {
                if (values.Length > 0 && values[0] != null) {
                    tx.Text = values[0];
                }
                return tx.GetAppearance();
            }
            if (!PdfName.CH.Equals(fieldType))
                throw new DocumentException("An appearance was requested without a variable text field.");
            PdfArray opt = merged.GetAsArray(PdfName.OPT);
            int flags = 0;
            PdfNumber nfl = merged.GetAsNumber(PdfName.FF);
            if (nfl != null)
                flags = nfl.IntValue;
            if ((flags & PdfFormField.FF_COMBO) != 0 && opt == null) {
                tx.Text = text;
                return tx.GetAppearance();
            }
            if (opt != null) {
                String[] choices = new String[opt.Size];
                String[] choicesExp = new String[opt.Size];
                for (int k = 0; k < opt.Size; ++k) {
                    PdfObject obj = opt[k];
                    if (obj.IsString()) {
                            choices[k] = choicesExp[k] = ((PdfString)obj).ToUnicodeString();
                    }
                    else {
                        PdfArray a = (PdfArray) obj;
                        choicesExp[k] = a.GetAsString(0).ToUnicodeString();
                        choices[k] = a.GetAsString(1).ToUnicodeString();
                    }
                }
                if ((flags & PdfFormField.FF_COMBO) != 0) {
                    for (int k = 0; k < choices.Length; ++k) {
                        if (text.Equals(choicesExp[k])) {
                            text = choices[k];
                            break;
                        }
                    }
                    tx.Text = text;
                    return tx.GetAppearance();
                }
                ArrayList indexes = new ArrayList();
                for (int k = 0; k < choicesExp.Length; ++k) {
                    for (int j = 0; j < values.Length; ++j) {
                        String val = values[j];
                        if (val != null && val.Equals(choicesExp[k])) {
                            indexes.Add(k);
                            break;
                        }
                    }
                }
                tx.Choices = choices;
                tx.ChoiceExports = choicesExp;
                tx.ChoiceSelections = indexes;
            }
            PdfAppearance app = tx.GetListAppearance();
            topFirst = tx.TopFirst;
            return app;
        }

        internal PdfAppearance GetAppearance(PdfDictionary merged, String text, String fieldName) {
            String[] valueArr = new String[]{text};
            return GetAppearance( merged, valueArr, fieldName );
        }

        internal Color GetMKColor(PdfArray ar) {
            if (ar == null)
                return null;
            switch (ar.Size) {
                case 1:
                    return new GrayColor(ar.GetAsNumber(0).FloatValue);
                case 3:
                    return new Color(ExtendedColor.Normalize(ar.GetAsNumber(0).FloatValue), ExtendedColor.Normalize(ar.GetAsNumber(1).FloatValue), ExtendedColor.Normalize(ar.GetAsNumber(2).FloatValue));
                case 4:
                    return new CMYKColor(ar.GetAsNumber(0).FloatValue, ar.GetAsNumber(1).FloatValue, ar.GetAsNumber(2).FloatValue, ar.GetAsNumber(3).FloatValue);
                default:
                    return null;
            }
        }
        
        /** Gets the field value.
        * @param name the fully qualified field name
        * @return the field value
        */    
        public String GetField(String name) {
            if (xfa.XfaPresent) {
                name = xfa.FindFieldName(name, this);
                if (name == null)
                    return null;
                name = XfaForm.Xml2Som.GetShortName(name);
                return XfaForm.GetNodeText(xfa.FindDatasetsNode(name));
            }
            Item item = (Item)fields[name];
            if (item == null)
                return null;
            lastWasString = false;
            PdfDictionary mergedDict = item.GetMerged( 0 );

            // Jose A. Rodriguez posted a fix to the mailing list (May 11, 2009)
            // explaining that the value can also be a stream value
            // the fix was made against an old iText version. Bruno adapted it.
            PdfObject v = PdfReader.GetPdfObject(mergedDict.Get(PdfName.V));
            if (v == null)
                return "";
            if (v is PRStream) {
                byte[] valBytes = PdfReader.GetStreamBytes((PRStream)v);
                return PdfEncodings.ConvertToString(valBytes, BaseFont.WINANSI);
            }
            
            PdfName type = mergedDict.GetAsName(PdfName.FT);
            if (PdfName.BTN.Equals(type)) {
                PdfNumber ff = mergedDict.GetAsNumber(PdfName.FF);
            int flags = 0;
                if (ff != null)
                    flags = ff.IntValue;
                if ((flags & PdfFormField.FF_PUSHBUTTON) != 0)
                    return "";
                String value = "";
                if (v is PdfName)
                    value = PdfName.DecodeName(v.ToString());
                else if (v is PdfString)
                    value = ((PdfString)v).ToUnicodeString();
                PdfArray opts = item.GetValue(0).GetAsArray(PdfName.OPT);
                if (opts != null) {
                    int idx = 0;
                    try {
                        idx = int.Parse(value);
                        PdfString ps = opts.GetAsString(idx);
                        value = ps.ToUnicodeString();
                        lastWasString = true;
                    }
                    catch {
                    }
                }
                return value;
            }
            if (v is PdfString) {
                lastWasString = true;
                return ((PdfString)v).ToUnicodeString();
            } else if (v is PdfName) {
                return PdfName.DecodeName(v.ToString());
            } else
                return "";
        }

        /**
        * Gets the field values of a Choice field.
        * @param name the fully qualified field name
        * @return the field value
        * @since 2.1.3
        */    
        public String[] GetListSelection(String name) {
            String[] ret;
            String s = GetField(name);
            if (s == null) {
                ret = new String[]{};
            }
            else {
                ret = new String[]{ s };
            }
            Item item = (Item)fields[name];
            if (item == null)
                return ret;
            PdfArray values = item.GetMerged(0).GetAsArray(PdfName.I);
            if (values == null)
                return ret;
            ret = new String[values.Size];
            String[] options = GetListOptionExport(name);
            int idx = 0;
            foreach (PdfNumber n in values.ArrayList) {
                ret[idx++] = options[n.IntValue];
            }
            return ret;
        }

        /**
        * Sets a field property. Valid property names are:
        * <p>
        * <ul>
        * <li>textfont - sets the text font. The value for this entry is a <CODE>BaseFont</CODE>.<br>
        * <li>textcolor - sets the text color. The value for this entry is a <CODE>java.awt.Color</CODE>.<br>
        * <li>textsize - sets the text size. The value for this entry is a <CODE>Float</CODE>.
        * <li>bgcolor - sets the background color. The value for this entry is a <CODE>java.awt.Color</CODE>.
        *     If <code>null</code> removes the background.<br>
        * <li>bordercolor - sets the border color. The value for this entry is a <CODE>java.awt.Color</CODE>.
        *     If <code>null</code> removes the border.<br>
        * </ul>
        * @param field the field name
        * @param name the property name
        * @param value the property value
        * @param inst an array of <CODE>int</CODE> indexing into <CODE>AcroField.Item.merged</CODE> elements to process.
        * Set to <CODE>null</CODE> to process all
        * @return <CODE>true</CODE> if the property exists, <CODE>false</CODE> otherwise
        */    
        public bool SetFieldProperty(String field, String name, Object value, int[] inst) {
            if (writer == null)
                throw new Exception("This AcroFields instance is read-only.");
            Item item = (Item)fields[field];
            if (item == null)
                return false;
            InstHit hit = new InstHit(inst);
            PdfDictionary merged;
            PdfString da;
            if (Util.EqualsIgnoreCase(name, "textfont")) {
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        merged = item.GetMerged( k );
                        da = merged.GetAsString(PdfName.DA);
                        PdfDictionary dr = merged.GetAsDict(PdfName.DR);
                        if (da != null && dr != null) {
                            Object[] dao = SplitDAelements(da.ToUnicodeString());
                            PdfAppearance cb = new PdfAppearance();
                            if (dao[DA_FONT] != null) {
                                BaseFont bf = (BaseFont)value;
                                PdfName psn = (PdfName)PdfAppearance.stdFieldFontNames[bf.PostscriptFontName];
                                if (psn == null) {
                                    psn = new PdfName(bf.PostscriptFontName);
                                }
                                PdfDictionary fonts = dr.GetAsDict(PdfName.FONT);
                                if (fonts == null) {
                                    fonts = new PdfDictionary();
                                    dr.Put(PdfName.FONT, fonts);
                                }
                                PdfIndirectReference fref = (PdfIndirectReference)fonts.Get(psn);
                                PdfDictionary top = reader.Catalog.GetAsDict(PdfName.ACROFORM);
                                MarkUsed(top);
                                dr = top.GetAsDict(PdfName.DR);
                                if (dr == null) {
                                    dr = new PdfDictionary();
                                    top.Put(PdfName.DR, dr);
                                }
                                MarkUsed(dr);
                                PdfDictionary fontsTop = dr.GetAsDict(PdfName.FONT);
                                if (fontsTop == null) {
                                    fontsTop = new PdfDictionary();
                                    dr.Put(PdfName.FONT, fontsTop);
                                }
                                MarkUsed(fontsTop);
                                PdfIndirectReference frefTop = (PdfIndirectReference)fontsTop.Get(psn);
                                if (frefTop != null) {
                                    if (fref == null)
                                        fonts.Put(psn, frefTop);
                                }
                                else if (fref == null) {
                                    FontDetails fd;
                                    if (bf.FontType == BaseFont.FONT_TYPE_DOCUMENT) {
                                        fd = new FontDetails(null, ((DocumentFont)bf).IndirectReference, bf);
                                    }
                                    else {
                                        bf.Subset = false;
                                        fd = writer.AddSimple(bf);
                                        localFonts[psn.ToString().Substring(1)] = bf;
                                    }
                                    fontsTop.Put(psn, fd.IndirectReference);
                                    fonts.Put(psn, fd.IndirectReference);
                                }
                                ByteBuffer buf = cb.InternalBuffer;
                                buf.Append(psn.GetBytes()).Append(' ').Append((float)dao[DA_SIZE]).Append(" Tf ");
                                if (dao[DA_COLOR] != null)
                                    cb.SetColorFill((Color)dao[DA_COLOR]);
                                PdfString s = new PdfString(cb.ToString());
                                item.GetMerged(k).Put(PdfName.DA, s);
                                item.GetWidget(k).Put(PdfName.DA, s);
                                MarkUsed(item.GetWidget(k));
                            }
                        }
                    }
                }
            }
            else if (Util.EqualsIgnoreCase(name, "textcolor")) {
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        merged = item.GetMerged( k );
                        da = merged.GetAsString(PdfName.DA);
                        if (da != null) {
                            Object[] dao = SplitDAelements(da.ToUnicodeString());
                            PdfAppearance cb = new PdfAppearance();
                            if (dao[DA_FONT] != null) {
                                ByteBuffer buf = cb.InternalBuffer;
                                buf.Append(new PdfName((String)dao[DA_FONT]).GetBytes()).Append(' ').Append((float)dao[DA_SIZE]).Append(" Tf ");
                                cb.SetColorFill((Color)value);
                                PdfString s = new PdfString(cb.ToString());
                                item.GetMerged(k).Put(PdfName.DA, s);
                                item.GetWidget(k).Put(PdfName.DA, s);
                                MarkUsed(item.GetWidget(k));
                            }
                        }
                    }
                }
            }
            else if (Util.EqualsIgnoreCase(name, "textsize")) {
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        merged = item.GetMerged( k );
                        da = merged.GetAsString(PdfName.DA);
                        if (da != null) {
                            Object[] dao = SplitDAelements(da.ToUnicodeString());
                            PdfAppearance cb = new PdfAppearance();
                            if (dao[DA_FONT] != null) {
                                ByteBuffer buf = cb.InternalBuffer;
                                buf.Append(new PdfName((String)dao[DA_FONT]).GetBytes()).Append(' ').Append((float)value).Append(" Tf ");
                                if (dao[DA_COLOR] != null)
                                    cb.SetColorFill((Color)dao[DA_COLOR]);
                                PdfString s = new PdfString(cb.ToString());
                                item.GetMerged(k).Put(PdfName.DA, s);
                                item.GetWidget(k).Put(PdfName.DA, s);
                                MarkUsed(item.GetWidget(k));
                            }
                        }
                    }
                }
            }
            else if (Util.EqualsIgnoreCase(name, "bgcolor") || Util.EqualsIgnoreCase(name, "bordercolor")) {
                PdfName dname = (Util.EqualsIgnoreCase(name, "bgcolor") ? PdfName.BG : PdfName.BC);
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        merged = item.GetMerged( k );
                        PdfDictionary mk = merged.GetAsDict(PdfName.MK);
                        if (mk == null) {
                            if (value == null)
                                return true;
                            mk = new PdfDictionary();
                            item.GetMerged(k).Put(PdfName.MK, mk);
                            item.GetWidget(k).Put(PdfName.MK, mk);
                            MarkUsed(item.GetWidget(k));
                        } else {
                            MarkUsed( mk );
                        }
                        if (value == null)
                            mk.Remove(dname);
                        else
                            mk.Put(dname, PdfFormField.GetMKColor((Color)value));
                    }
                }
            }
            else
                return false;
            return true;
        }

        /**
        * Sets a field property. Valid property names are:
        * <p>
        * <ul>
        * <li>flags - a set of flags specifying various characteristics of the field�s widget annotation.
        * The value of this entry replaces that of the F entry in the form�s corresponding annotation dictionary.<br>
        * <li>setflags - a set of flags to be set (turned on) in the F entry of the form�s corresponding
        * widget annotation dictionary. Bits equal to 1 cause the corresponding bits in F to be set to 1.<br>
        * <li>clrflags - a set of flags to be cleared (turned off) in the F entry of the form�s corresponding
        * widget annotation dictionary. Bits equal to 1 cause the corresponding
        * bits in F to be set to 0.<br>
        * <li>fflags - a set of flags specifying various characteristics of the field. The value
        * of this entry replaces that of the Ff entry in the form�s corresponding field dictionary.<br>
        * <li>setfflags - a set of flags to be set (turned on) in the Ff entry of the form�s corresponding
        * field dictionary. Bits equal to 1 cause the corresponding bits in Ff to be set to 1.<br>
        * <li>clrfflags - a set of flags to be cleared (turned off) in the Ff entry of the form�s corresponding
        * field dictionary. Bits equal to 1 cause the corresponding bits in Ff
        * to be set to 0.<br>
        * </ul>
        * @param field the field name
        * @param name the property name
        * @param value the property value
        * @param inst an array of <CODE>int</CODE> indexing into <CODE>AcroField.Item.merged</CODE> elements to process.
        * Set to <CODE>null</CODE> to process all
        * @return <CODE>true</CODE> if the property exists, <CODE>false</CODE> otherwise
        */    
        public bool SetFieldProperty(String field, String name, int value, int[] inst) {
            if (writer == null)
                throw new Exception("This AcroFields instance is read-only.");
            Item item = (Item)fields[field];
            if (item == null)
                return false;
            InstHit hit = new InstHit(inst);
            if (Util.EqualsIgnoreCase(name, "flags")) {
                PdfNumber num = new PdfNumber(value);
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        item.GetMerged(k).Put(PdfName.F, num);
                        item.GetWidget(k).Put(PdfName.F, num);
                        MarkUsed(item.GetWidget(k));
                    }
                }
            }
            else if (Util.EqualsIgnoreCase(name, "setflags")) {
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        PdfNumber num = item.GetWidget(k).GetAsNumber(PdfName.F);
                        int val = 0;
                        if (num != null)
                            val = num.IntValue;
                        num = new PdfNumber(val | value);
                        item.GetMerged(k).Put(PdfName.F, num);
                        item.GetWidget(k).Put(PdfName.F, num);
                        MarkUsed(item.GetWidget(k));
                    }
                }
            }
            else if (Util.EqualsIgnoreCase(name, "clrflags")) {
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        PdfDictionary widget = item.GetWidget( k );
                        PdfNumber num = widget.GetAsNumber(PdfName.F);
                        int val = 0;
                        if (num != null)
                            val = num.IntValue;
                        num = new PdfNumber(val & (~value));
                        item.GetMerged(k).Put(PdfName.F, num);
                        widget.Put(PdfName.F, num);
                        MarkUsed(widget);
                    }
                }
            }
            else if (Util.EqualsIgnoreCase(name, "fflags")) {
                PdfNumber num = new PdfNumber(value);
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        item.GetMerged(k).Put(PdfName.FF, num);
                        item.GetValue(k).Put(PdfName.FF, num);
                        MarkUsed(item.GetValue(k));
                    }
                }
            }
            else if (Util.EqualsIgnoreCase(name, "setfflags")) {
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        PdfDictionary valDict = item.GetValue( k );
                        PdfNumber num = valDict.GetAsNumber( PdfName.FF );
                        int val = 0;
                        if (num != null)
                            val = num.IntValue;
                        num = new PdfNumber(val | value);
                        item.GetMerged(k).Put(PdfName.FF, num);
                        valDict.Put(PdfName.FF, num);
                        MarkUsed(valDict);
                    }
                }
            }
            else if (Util.EqualsIgnoreCase(name, "clrfflags")) {
                for (int k = 0; k < item.Size; ++k) {
                    if (hit.IsHit(k)) {
                        PdfDictionary valDict = item.GetValue( k );
                        PdfNumber num = valDict.GetAsNumber(PdfName.FF);
                        int val = 0;
                        if (num != null)
                            val = num.IntValue;
                        num = new PdfNumber(val & (~value));
                        item.GetMerged(k).Put(PdfName.FF, num);
                        valDict.Put(PdfName.FF, num);
                        MarkUsed(valDict);
                    }
                }
            }
            else
                return false;
            return true;
        }
        
        /**
        * Merges an XML data structure into this form.
        * @param n the top node of the data structure
        * @throws java.io.IOException on error
        * @throws com.lowagie.text.DocumentException o error
        */
        public void MergeXfaData(XmlNode n) {
            XfaForm.Xml2SomDatasets data = new XfaForm.Xml2SomDatasets(n);
            foreach (String name in data.Order) {
                String text = XfaForm.GetNodeText((XmlNode)data.Name2Node[name]);
                SetField(name, text);
            }
        }

        /** Sets the fields by FDF merging.
        * @param fdf the FDF form
        * @throws IOException on error
        * @throws DocumentException on error
        */    
        public void SetFields(FdfReader fdf) {
            Hashtable fd = fdf.Fields;
            foreach (string f in fd.Keys) {
                String v = fdf.GetFieldValue(f);
                if (v != null)
                    SetField(f, v);
            }
        }
        
        /** Sets the fields by XFDF merging.
        * @param xfdf the XFDF form
        * @throws IOException on error
        * @throws DocumentException on error
        */
        
        public void SetFields(XfdfReader xfdf) {
            Hashtable fd = xfdf.Fields;
            foreach (string f in fd.Keys) {
                String v = xfdf.GetFieldValue(f);
                if (v != null)
                    SetField(f, v);
                ArrayList l = xfdf.GetListValues(f);
                if (l != null) {
                    string[] ar = (string[])l.ToArray(typeof(string[]));
                    SetListSelection(v, ar);
                }
            }
        }

        /**
        * Regenerates the field appearance.
        * This is usefull when you change a field property, but not its value,
        * for instance form.SetFieldProperty("f", "bgcolor", Color.BLUE, null);
        * This won't have any effect, unless you use RegenerateField("f") after changing
        * the property.
        * 
        * @param name the fully qualified field name or the partial name in the case of XFA forms
        * @throws IOException on error
        * @throws DocumentException on error
        * @return <CODE>true</CODE> if the field was found and changed,
        * <CODE>false</CODE> otherwise
        */    
        public bool RegenerateField(String name) {
    	    String value = GetField(name);
            return SetField(name, value, value);
        }

        /** Sets the field value.
        * @param name the fully qualified field name or the partial name in the case of XFA forms
        * @param value the field value
        * @throws IOException on error
        * @throws DocumentException on error
        * @return <CODE>true</CODE> if the field was found and changed,
        * <CODE>false</CODE> otherwise
        */    
        public bool SetField(String name, String value) {
            return SetField(name, value, null);
        }
        
        /** Sets the field value and the display string. The display string
        * is used to build the appearance in the cases where the value
        * is modified by Acrobat with JavaScript and the algorithm is
        * known.
        * @param name the fully qualified field name or the partial name in the case of XFA forms
        * @param value the field value
        * @param display the string that is used for the appearance. If <CODE>null</CODE>
        * the <CODE>value</CODE> parameter will be used
        * @return <CODE>true</CODE> if the field was found and changed,
        * <CODE>false</CODE> otherwise
        * @throws IOException on error
        * @throws DocumentException on error
        */    
        public bool SetField(String name, String value, String display) {
            if (writer == null)
                throw new DocumentException("This AcroFields instance is read-only.");
            if (xfa.XfaPresent) {
                name = xfa.FindFieldName(name, this);
                if (name == null)
                    return false;
                String shortName = XfaForm.Xml2Som.GetShortName(name);
                XmlNode xn = xfa.FindDatasetsNode(shortName);
                if (xn == null) {
                    xn = xfa.DatasetsSom.InsertNode(xfa.DatasetsNode, shortName);
                }
                xfa.SetNodeText(xn, value);
            }
            Item item = (Item)fields[name];
            if (item == null)
                return false;
            PdfDictionary merged = item.GetMerged( 0 );
            PdfName type = merged.GetAsName(PdfName.FT);
            if (PdfName.TX.Equals(type)) {
                PdfNumber maxLen = merged.GetAsNumber(PdfName.MAXLEN);
                int len = 0;
                if (maxLen != null)
                    len = maxLen.IntValue;
                if (len > 0)
                    value = value.Substring(0, Math.Min(len, value.Length));
            }
            if (display == null)
                display = value;
            if (PdfName.TX.Equals(type) || PdfName.CH.Equals(type)) {
                PdfString v = new PdfString(value, PdfObject.TEXT_UNICODE);
                for (int idx = 0; idx < item.Size; ++idx) {
                    PdfDictionary valueDic = item.GetValue(idx);
                    valueDic.Put(PdfName.V, v);
                    valueDic.Remove(PdfName.I);
                    MarkUsed(valueDic);                
                    merged = item.GetMerged(idx);
                    merged.Remove(PdfName.I);
                    merged.Put(PdfName.V, v);
                    PdfDictionary widget = item.GetWidget(idx);
                    if (generateAppearances) {
                        PdfAppearance app = GetAppearance(merged, display, name);
                        if (PdfName.CH.Equals(type)) {
                            PdfNumber n = new PdfNumber(topFirst);
                            widget.Put(PdfName.TI, n);
                            merged.Put(PdfName.TI, n);
                        }
                        PdfDictionary appDic = widget.GetAsDict(PdfName.AP);
                        if (appDic == null) {
                            appDic = new PdfDictionary();
                            widget.Put(PdfName.AP, appDic);
                            merged.Put(PdfName.AP, appDic);
                        }
                        appDic.Put(PdfName.N, app.IndirectReference);
                        writer.ReleaseTemplate(app);
                    }
                    else {
                        widget.Remove(PdfName.AP);
                        merged.Remove(PdfName.AP);
                    }
                    MarkUsed(widget);
                }
                return true;
            }
            else if (PdfName.BTN.Equals(type)) {
                PdfNumber ff = item.GetMerged(0).GetAsNumber(PdfName.FF);
                int flags = 0;
                if (ff != null)
                    flags = ff.IntValue;
                if ((flags & PdfFormField.FF_PUSHBUTTON) != 0) {
                    //we'll assume that the value is an image in base64
                    Image img;
                    try {
                        img = Image.GetInstance(Convert.FromBase64String(value));
                    }
                    catch {
                        return false;
                    }
                    PushbuttonField pb = GetNewPushbuttonFromField(name);
                    pb.Image = img;
                    ReplacePushbuttonField(name, pb.Field);
                    return true;
                }
                PdfName v = new PdfName(value);
                ArrayList lopt = new ArrayList();
                PdfArray opts = item.GetValue(0).GetAsArray(PdfName.OPT);
                if (opts != null) {
                    for (int k = 0; k < opts.Size; ++k) {
                        PdfString valStr = opts.GetAsString(k);
                        if (valStr != null)
                            lopt.Add(valStr.ToUnicodeString());
                        else
                            lopt.Add(null);
                    }
                }
                int vidx = lopt.IndexOf(value);
                PdfName vt;
                if (vidx >= 0)
                    vt = new PdfName(vidx.ToString());
                else
                    vt = v;
                for (int idx = 0; idx < item.Size; ++idx) {
                    merged = item.GetMerged(idx);
                    PdfDictionary widget = item.GetWidget(idx);
                    PdfDictionary valDict = item.GetValue(idx);
                    MarkUsed(item.GetValue(idx));
                    valDict.Put(PdfName.V, vt);
                    merged.Put(PdfName.V, vt);
                    MarkUsed(widget);
                    if (IsInAP(widget,  vt)) {
                        merged.Put(PdfName.AS, vt);
                        widget.Put(PdfName.AS, vt);
                    }
                    else {
                        merged.Put(PdfName.AS, PdfName.Off_);
                        widget.Put(PdfName.AS, PdfName.Off_);
                    }
                }
                return true;
            }
            return false;
        }
        
        /**
        * Sets different values in a list selection.
        * No appearance is generated yet; nor does the code check if multiple select is allowed.
        * 
        * @param    name    the name of the field
        * @param    value   an array with values that need to be selected
        * @return   true only if the field value was changed
        * @since 2.1.4
        */
        public bool SetListSelection(String name, String[] value) {
            Item item = GetFieldItem(name);
            if (item == null)
                return false;

            PdfDictionary merged = item.GetMerged(0);
            PdfName type = merged.GetAsName(PdfName.FT);

            if (!PdfName.CH.Equals(type)) {
                return false;
            }
            String[] options = GetListOptionExport(name);
            PdfArray array = new PdfArray();
            for (int i = 0; i < value.Length; i++) {
                for (int j = 0; j < options.Length; j++) {
                    if (options[j].Equals(value[i])) {
                        array.Add(new PdfNumber(j));
                    }
                }
            }
            item.WriteToAll(PdfName.I, array, Item.WRITE_MERGED | Item.WRITE_VALUE);

            PdfArray vals = new PdfArray();
            for (int i = 0; i < value.Length; ++i) {
                vals.Add( new PdfString( value[i] ) );
            }
            item.WriteToAll(PdfName.V, vals, Item.WRITE_MERGED | Item.WRITE_VALUE);

            PdfAppearance app = GetAppearance(merged, value, name );

            PdfDictionary apDic = new PdfDictionary();
            apDic.Put( PdfName.N, app.IndirectReference);
            item.WriteToAll(PdfName.AP, apDic, Item.WRITE_MERGED | Item.WRITE_WIDGET);

            writer.ReleaseTemplate( app );

            item.MarkUsed( this, Item.WRITE_VALUE | Item.WRITE_WIDGET );
            return true;
        }

        internal bool IsInAP(PdfDictionary dic, PdfName check) {
            PdfDictionary appDic = dic.GetAsDict(PdfName.AP);
            if (appDic == null)
                return false;
            PdfDictionary NDic = appDic.GetAsDict(PdfName.N);
            return (NDic != null && NDic.Get(check) != null);
        }
        
        /** Gets all the fields. The fields are keyed by the fully qualified field name and
        * the value is an instance of <CODE>AcroFields.Item</CODE>.
        * @return all the fields
        */    
        public Hashtable Fields {
            get {
                return fields;
            }
        }
        
        /**
        * Gets the field structure.
        * @param name the name of the field
        * @return the field structure or <CODE>null</CODE> if the field
        * does not exist
        */    
        public Item GetFieldItem(String name) {
            if (xfa.XfaPresent) {
                name = xfa.FindFieldName(name, this);
                if (name == null)
                    return null;
            }
            return (Item)fields[name];
        }

        /**
        * Gets the long XFA translated name.
        * @param name the name of the field
        * @return the long field name
        */    
        public String GetTranslatedFieldName(String name) {
            if (xfa.XfaPresent) {
                String namex = xfa.FindFieldName(name, this);
                if (namex != null)
                    name = namex;
            }
            return name;
        }
        
        /**
        * Gets the field box positions in the document. The return is an array of <CODE>float</CODE>
        * multiple of 5. For each of this groups the values are: [page, llx, lly, urx,
        * ury]. The coordinates have the page rotation in consideration.
        * @param name the field name
        * @return the positions or <CODE>null</CODE> if field does not exist
        */    
        public float[] GetFieldPositions(String name) {
            Item item = GetFieldItem(name);
            if (item == null)
                return null;
            float[] ret = new float[item.Size * 5];
            int ptr = 0;
            for (int k = 0; k < item.Size; ++k) {
                try {
                    PdfDictionary wd = item.GetWidget(k);
                    PdfArray rect = wd.GetAsArray(PdfName.RECT);
                    if (rect == null)
                        continue;
                    Rectangle r = PdfReader.GetNormalizedRectangle(rect);
                    int page = item.GetPage(k);
                    int rotation = reader.GetPageRotation(page);
                    ret[ptr++] = page;
                    if (rotation != 0) {
                        Rectangle pageSize = reader.GetPageSize(page);
                        switch (rotation) {
                            case 270:
                                r = new Rectangle(
                                    pageSize.Top - r.Bottom,
                                    r.Left,
                                    pageSize.Top - r.Top,
                                    r.Right);
                                break;
                            case 180:
                                r = new Rectangle(
                                    pageSize.Right - r.Left,
                                    pageSize.Top - r.Bottom,
                                    pageSize.Right - r.Right,
                                    pageSize.Top - r.Top);
                                break;
                            case 90:
                                r = new Rectangle(
                                    r.Bottom,
                                    pageSize.Right - r.Left,
                                    r.Top,
                                    pageSize.Right - r.Right);
                                break;
                        }
                        r.Normalize();
                    }
                    ret[ptr++] = r.Left;
                    ret[ptr++] = r.Bottom;
                    ret[ptr++] = r.Right;
                    ret[ptr++] = r.Top;
                }
                catch {
                    // empty on purpose
                }
            }
            if (ptr < ret.Length) {
                float[] ret2 = new float[ptr];
                System.Array.Copy(ret, 0, ret2, 0, ptr);
                return ret2;
            }
            return ret;
        }
        
        private int RemoveRefFromArray(PdfArray array, PdfObject refo) {
            if (refo == null || !refo.IsIndirect())
                return array.Size;
            PdfIndirectReference refi = (PdfIndirectReference)refo;
            for (int j = 0; j < array.Size; ++j) {
                PdfObject obj = array[j];
                if (!obj.IsIndirect())
                    continue;
                if (((PdfIndirectReference)obj).Number == refi.Number)
                    array.Remove(j--);
            }
            return array.Size;
        }
        
        /**
        * Removes all the fields from <CODE>page</CODE>.
        * @param page the page to remove the fields from
        * @return <CODE>true</CODE> if any field was removed, <CODE>false otherwise</CODE>
        */    
        public bool RemoveFieldsFromPage(int page) {
            if (page < 1)
                return false;
            String[] names = new String[fields.Count];
            fields.Keys.CopyTo(names, 0);
            bool found = false;
            for (int k = 0; k < names.Length; ++k) {
                bool fr = RemoveField(names[k], page);
                found = (found || fr);
            }
            return found;
        }
        
        /**
        * Removes a field from the document. If page equals -1 all the fields with this
        * <CODE>name</CODE> are removed from the document otherwise only the fields in
        * that particular page are removed.
        * @param name the field name
        * @param page the page to remove the field from or -1 to remove it from all the pages
        * @return <CODE>true</CODE> if the field exists, <CODE>false otherwise</CODE>
        */    
        public bool RemoveField(String name, int page) {
            Item item = GetFieldItem(name);
            if (item == null)
                return false;
            PdfDictionary acroForm = (PdfDictionary)PdfReader.GetPdfObject(reader.Catalog.Get(PdfName.ACROFORM), reader.Catalog);
            
            if (acroForm == null)
                return false;
            PdfArray arrayf = acroForm.GetAsArray(PdfName.FIELDS);
            if (arrayf == null)
                return false;
            for (int k = 0; k < item.Size; ++k) {
                int pageV = item.GetPage(k);
                if (page != -1 && page != pageV)
                    continue;
                PdfIndirectReference refi = item.GetWidgetRef(k);
                PdfDictionary wd = item.GetWidget( k );
                PdfDictionary pageDic = reader.GetPageN(pageV);
                PdfArray annots = pageDic.GetAsArray(PdfName.ANNOTS);
                if (annots != null) {
                    if (RemoveRefFromArray(annots, refi) == 0) {
                        pageDic.Remove(PdfName.ANNOTS);
                        MarkUsed(pageDic);
                    }
                    else
                        MarkUsed(annots);
                }
                PdfReader.KillIndirect(refi);
                PdfIndirectReference kid = refi;
                while ((refi = wd.GetAsIndirectObject(PdfName.PARENT)) != null) {
                    wd = wd.GetAsDict( PdfName.PARENT );
                    PdfArray kids = wd.GetAsArray(PdfName.KIDS);
                    if (RemoveRefFromArray(kids, kid) != 0)
                        break;
                    kid = refi;
                    PdfReader.KillIndirect(refi);
                }
                if (refi == null) {
                    RemoveRefFromArray(arrayf, kid);
                    MarkUsed(arrayf);
                }
                if (page != -1) {
                    item.Remove( k );
                    --k;
                }
            }
            if (page == -1 || item.Size == 0)
                fields.Remove(name);
            return true;
        }
        
        /**
        * Removes a field from the document.
        * @param name the field name
        * @return <CODE>true</CODE> if the field exists, <CODE>false otherwise</CODE>
        */    
        public bool RemoveField(String name) {
            return RemoveField(name, -1);
        }
        
        /** Sets the option to generate appearances. Not generating apperances
        * will speed-up form filling but the results can be
        * unexpected in Acrobat. Don't use it unless your environment is well
        * controlled. The default is <CODE>true</CODE>.
        * @param generateAppearances the option to generate appearances
        */
        public bool GenerateAppearances {
            set {
                generateAppearances = value;
                PdfDictionary top = reader.Catalog.GetAsDict(PdfName.ACROFORM);
                if (generateAppearances)
                    top.Remove(PdfName.NEEDAPPEARANCES);
                else
                    top.Put(PdfName.NEEDAPPEARANCES, PdfBoolean.PDFTRUE);
            }
            get {
                return generateAppearances;
            }
        }
        
        /** The field representations for retrieval and modification. */    
        public class Item {
            
            /**
            * <CODE>writeToAll</CODE> constant.
            * 
            *  @since 2.1.5
            */
            public const int WRITE_MERGED = 1;
            
            /**
            * <CODE>writeToAll</CODE> and <CODE>markUsed</CODE> constant.
            * 
            *  @since 2.1.5
            */
            public const int WRITE_WIDGET = 2;
            
            /**
            * <CODE>writeToAll</CODE> and <CODE>markUsed</CODE> constant.
            * 
            *  @since 2.1.5
            */
            public const int WRITE_VALUE = 4;

            /**
            * This function writes the given key/value pair to all the instances
            * of merged, widget, and/or value, depending on the <code>writeFlags</code> setting
            *
            * @since 2.1.5
            *
            * @param key        you'll never guess what this is for.
            * @param value      if value is null, the key will be removed
            * @param writeFlags ORed together WRITE_* flags
            */
            public void WriteToAll(PdfName key, PdfObject value, int writeFlags) {
                int i;
                PdfDictionary curDict = null;
                if ((writeFlags & WRITE_MERGED) != 0) {
                    for (i = 0; i < merged.Count; ++i) {
                        curDict = GetMerged(i);
                        curDict.Put(key, value);
                    }
                }
                if ((writeFlags & WRITE_WIDGET) != 0) {
                    for (i = 0; i < widgets.Count; ++i) {
                        curDict = GetWidget(i);
                        curDict.Put(key, value);
                    }
                }
                if ((writeFlags & WRITE_VALUE) != 0) {
                    for (i = 0; i < values.Count; ++i) {
                        curDict = GetValue(i);
                        curDict.Put(key, value);
                    }
                }
            }

            /**
            * Mark all the item dictionaries used matching the given flags
            * 
            * @since 2.1.5
            * @param writeFlags WRITE_MERGED is ignored
            */
            public void MarkUsed( AcroFields parentFields, int writeFlags ) {
                if ((writeFlags & WRITE_VALUE) != 0) {
                    for (int i = 0; i < Size; ++i) {
                        parentFields.MarkUsed( GetValue( i ) );
                    }
                }
                if ((writeFlags & WRITE_WIDGET) != 0) {
                    for (int i = 0; i < Size; ++i) {
                        parentFields.MarkUsed(GetWidget(i));
                    }
                }
            }

            /**
            * An array of <CODE>PdfDictionary</CODE> where the value tag /V
            * is present.
            * 
            * @deprecated (will remove 'public' in the future)
            */
            public ArrayList values = new ArrayList();
            
            /**
            * An array of <CODE>PdfDictionary</CODE> with the widgets.
            * 
            * @deprecated (will remove 'public' in the future)
            */
            public ArrayList widgets = new ArrayList();
            
            /**
            * An array of <CODE>PdfDictionary</CODE> with the widget references.
            * 
            * @deprecated (will remove 'public' in the future)
            */
            public ArrayList widget_refs = new ArrayList();
            
            /**
            * An array of <CODE>PdfDictionary</CODE> with all the field
            * and widget tags merged.
            * 
            * @deprecated (will remove 'public' in the future)
            */
            public ArrayList merged = new ArrayList();
            
            /**
            * An array of <CODE>Integer</CODE> with the page numbers where
            * the widgets are displayed.
            * 
            * @deprecated (will remove 'public' in the future)
            */
            public ArrayList page = new ArrayList();
            /**
            * An array of <CODE>Integer</CODE> with the tab order of the field in the page.
            * 
            * @deprecated (will remove 'public' in the future)
            */
            public ArrayList tabOrder = new ArrayList();

            /**
            * Preferred method of determining the number of instances
            * of a given field.
            * 
            * @since 2.1.5
            * @return number of instances
            */
            public int Size {
                get {
                    return values.Count;
                }
            }

            /**
            * Remove the given instance from this item.  It is possible to
            * remove all instances using this function.
            * 
            * @since 2.1.5
            * @param killIdx
            */
            internal void Remove(int killIdx) {
                values.RemoveAt(killIdx);
                widgets.RemoveAt(killIdx);
                widget_refs.RemoveAt(killIdx);
                merged.RemoveAt(killIdx);
                page.RemoveAt(killIdx);
                tabOrder.RemoveAt(killIdx);
            }

            /**
            * Retrieve the value dictionary of the given instance
            * 
            * @since 2.1.5
            * @param idx instance index
            * @return dictionary storing this instance's value.  It may be shared across instances.
            */
            public PdfDictionary GetValue(int idx) {
                return (PdfDictionary) values[idx];
            }

            /**
            * Add a value dict to this Item
            * 
            * @since 2.1.5
            * @param value new value dictionary
            */
            internal void AddValue(PdfDictionary value) {
                values.Add(value);
            }

            /**
            * Retrieve the widget dictionary of the given instance
            * 
            * @since 2.1.5
            * @param idx instance index
            * @return The dictionary found in the appropriate page's Annot array.
            */
            public PdfDictionary GetWidget(int idx) {
                return (PdfDictionary) widgets[idx];
            }

            /**
            * Add a widget dict to this Item
            * 
            * @since 2.1.5
            * @param widget
            */
            internal void AddWidget(PdfDictionary widget) {
                widgets.Add(widget);
            }

            /**
            * Retrieve the reference to the given instance
            * 
            * @since 2.1.5
            * @param idx instance index
            * @return reference to the given field instance
            */
            public PdfIndirectReference GetWidgetRef(int idx) {
                return (PdfIndirectReference) widget_refs[idx];
            }

            /**
            * Add a widget ref to this Item
            * 
            * @since 2.1.5
            * @param widgRef
            */
            internal void AddWidgetRef(PdfIndirectReference widgRef) {
                widget_refs.Add(widgRef);
            }

            /**
            * Retrieve the merged dictionary for the given instance.  The merged
            * dictionary contains all the keys present in parent fields, though they
            * may have been overwritten (or modified?) by children.
            * Example: a merged radio field dict will contain /V
            * 
            * @since 2.1.5
            * @param idx  instance index
            * @return the merged dictionary for the given instance
            */
            public PdfDictionary GetMerged(int idx) {
                return (PdfDictionary) merged[idx];
            }

            /**
            * Adds a merged dictionary to this Item.
            * 
            * @since 2.1.5
            * @param mergeDict
            */
            internal void AddMerged(PdfDictionary mergeDict) {
                merged.Add(mergeDict);
            }

            /**
            * Retrieve the page number of the given instance
            * 
            * @since 2.1.5
            * @param idx
            * @return remember, pages are "1-indexed", not "0-indexed" like field instances.
            */
            public int GetPage(int idx) {
                return (int) page[idx];
            }

            /**
            * Adds a page to the current Item.
            * 
            * @since 2.1.5
            * @param pg
            */
            internal void AddPage(int pg) {
                page.Add(pg);
            }

            /**
            * forces a page value into the Item.
            * 
            * @since 2.1.5
            * @param idx
            */
            internal void ForcePage(int idx, int pg) {
                page[idx] = pg;
            }

            /**
            * Gets the tabOrder.
            * 
            * @since 2.1.5
            * @param idx
            * @return tab index of the given field instance
            */
            public int GetTabOrder(int idx) {
                return (int) tabOrder[idx];
            }

            /**
            * Adds a tab order value to this Item.
            * 
            * @since 2.1.5
            * @param order
            */
            internal void AddTabOrder(int order) {
                tabOrder.Add(order);
            }
        }
        
        private class InstHit {
            IntHashtable hits;
            public InstHit(int[] inst) {
                if (inst == null)
                    return;
                hits = new IntHashtable();
                for (int k = 0; k < inst.Length; ++k)
                    hits[inst[k]] = 1;
            }
            
            public bool IsHit(int n) {
                if (hits == null)
                    return true;
                return hits.ContainsKey(n);
            }
        }

        private void FindSignatureNames() {
            if (sigNames != null)
                return;
            sigNames = new Hashtable();
            ArrayList sorter = new ArrayList();
            foreach (DictionaryEntry entry in fields) {
                Item item = (Item)entry.Value;
                PdfDictionary merged = item.GetMerged(0);
                if (!PdfName.SIG.Equals(merged.Get(PdfName.FT)))
                    continue;
                PdfDictionary v = merged.GetAsDict(PdfName.V);
                if (v == null)
                    continue;
                PdfString contents = v.GetAsString(PdfName.CONTENTS);
                if (contents == null)
                    continue;
                PdfArray ro = v.GetAsArray(PdfName.BYTERANGE);
                if (ro == null)
                    continue;
                int rangeSize = ro.Size;
                if (rangeSize < 2)
                    continue;
                int length = ro.GetAsNumber(rangeSize - 1).IntValue + ro.GetAsNumber(rangeSize - 2).IntValue;
                sorter.Add(new Object[]{entry.Key, new int[]{length, 0}});
            }
            sorter.Sort(new AcroFields.ISorterComparator());
            if (sorter.Count > 0) {
                if (((int[])((Object[])sorter[sorter.Count - 1])[1])[0] == reader.FileLength)
                    totalRevisions = sorter.Count;
                else
                    totalRevisions = sorter.Count + 1;
                for (int k = 0; k < sorter.Count; ++k) {
                    Object[] objs = (Object[])sorter[k];
                    String name = (String)objs[0];
                    int[] p = (int[])objs[1];
                    p[1] = k + 1;
                    sigNames[name] = p;
                }
            }
        }

        /**
        * Gets the field names that have signatures and are signed.
        * @return the field names that have signatures and are signed
        */    
        public IEnumerable<string> GetSignatureNames() {
            FindSignatureNames();
            return sigNames.Keys.Cast<string>().ToArray();
        }
        
        /**
        * Gets the field names that have blank signatures.
        * @return the field names that have blank signatures
        */    
        public ArrayList GetBlankSignatureNames() {
            FindSignatureNames();
            ArrayList sigs = new ArrayList();
            foreach (DictionaryEntry entry in fields) {
                Item item = (Item)entry.Value;
                PdfDictionary merged = item.GetMerged(0);
                if (!PdfName.SIG.Equals(merged.GetAsName(PdfName.FT)))
                    continue;
                if (sigNames.ContainsKey(entry.Key))
                    continue;
                sigs.Add(entry.Key);
            }
            return sigs;
        }

	private void ClearSignature(PdfDictionary dic) {
	    dic.Remove(PdfName.AP);
	    dic.Remove(PdfName.AS);
	    dic.Remove(PdfName.V);
	    dic.Remove(PdfName.DV);
	    dic.Remove(PdfName.SV);
	    dic.Remove(PdfName.FF);
	    dic.Put(PdfName.F, new PdfNumber(PdfAnnotation.FLAGS_PRINT));
	}

	public bool ClearSignatureField(string name) {
	    sigNames = null;
	    FindSignatureNames();
	    if (!sigNames.ContainsKey(name))
		return false;
	    var sig = (Item)fields[name];
	    sig.MarkUsed(this, Item.WRITE_VALUE | Item.WRITE_WIDGET);
	    for (int k = 0; k < sig.Size; ++k) {
		ClearSignature(sig.GetMerged(k));
		ClearSignature(sig.GetWidget(k));
		ClearSignature(sig.GetValue(k));
	    }

	    return true;
	}
        
        /**
        * Gets the signature dictionary, the one keyed by /V.
        * @param name the field name
        * @return the signature dictionary keyed by /V or <CODE>null</CODE> if the field is not
        * a signature
        */    
        public PdfDictionary GetSignatureDictionary(String name) {
            FindSignatureNames();
            name = GetTranslatedFieldName(name);
            if (!sigNames.ContainsKey(name))
                return null;
            Item item = (Item)fields[name];
            PdfDictionary merged = item.GetMerged(0);
            return merged.GetAsDict(PdfName.V);
        }
        
        /**
        * Checks is the signature covers the entire document or just part of it.
        * @param name the signature field name
        * @return <CODE>true</CODE> if the signature covers the entire document,
        * <CODE>false</CODE> otherwise
        */    
        public bool SignatureCoversWholeDocument(String name) {
            FindSignatureNames();
            name = GetTranslatedFieldName(name);
            if (!sigNames.ContainsKey(name))
                return false;
            return ((int[])sigNames[name])[0] == reader.FileLength;
        }
        
        /**
        * Verifies a signature. An example usage is:
        * <p>
        * <pre>
        * KeyStore kall = PdfPKCS7.LoadCacertsKeyStore();
        * PdfReader reader = new PdfReader("my_signed_doc.pdf");
        * AcroFields af = reader.GetAcroFields();
        * ArrayList names = af.GetSignatureNames();
        * for (int k = 0; k &lt; names.Size(); ++k) {
        *    String name = (String)names.Get(k);
        *    System.out.Println("Signature name: " + name);
        *    System.out.Println("Signature covers whole document: " + af.SignatureCoversWholeDocument(name));
        *    PdfPKCS7 pk = af.VerifySignature(name);
        *    Calendar cal = pk.GetSignDate();
        *    Certificate pkc[] = pk.GetCertificates();
        *    System.out.Println("Subject: " + PdfPKCS7.GetSubjectFields(pk.GetSigningCertificate()));
        *    System.out.Println("Document modified: " + !pk.Verify());
        *    Object fails[] = PdfPKCS7.VerifyCertificates(pkc, kall, null, cal);
        *    if (fails == null)
        *        System.out.Println("Certificates verified against the KeyStore");
        *    else
        *        System.out.Println("Certificate failed: " + fails[1]);
        * }
        * </pre>
        * @param name the signature field name
        * @return a <CODE>PdfPKCS7</CODE> class to continue the verification
        */    
        public PdfPKCS7 VerifySignature(String name) {
            PdfDictionary v = GetSignatureDictionary(name);
            if (v == null)
                return null;
            PdfName sub = v.GetAsName(PdfName.SUBFILTER);
            PdfString contents = v.GetAsString(PdfName.CONTENTS);
            PdfPKCS7 pk = null;
            if (sub.Equals(PdfName.ADBE_X509_RSA_SHA1)) {
                PdfString cert = v.GetAsString(PdfName.CERT);
                pk = new PdfPKCS7(contents.GetOriginalBytes(), cert.GetBytes());
            }
            else
                pk = new PdfPKCS7(contents.GetOriginalBytes());
            UpdateByteRange(pk, v);
            PdfString str = v.GetAsString(PdfName.M);
            if (str != null)
                pk.SignDate = PdfDate.Decode(str.ToString());
            PdfObject obj = PdfReader.GetPdfObject(v.Get(PdfName.NAME));
            if (obj != null) {
              if (obj.IsString())
                pk.SignName = ((PdfString)obj).ToUnicodeString();
              else if(obj.IsName())
                pk.SignName = PdfName.DecodeName(obj.ToString());
            }
            str = v.GetAsString(PdfName.REASON);
            if (str != null)
                pk.Reason = str.ToUnicodeString();
            str = v.GetAsString(PdfName.LOCATION);
            if (str != null)
                pk.Location = str.ToUnicodeString();
            return pk;
        }
        
        private void UpdateByteRange(PdfPKCS7 pkcs7, PdfDictionary v) {
            PdfArray b = v.GetAsArray(PdfName.BYTERANGE);
            RandomAccessFileOrArray rf = reader.SafeFile;
            try {
                rf.ReOpen();
                byte[] buf = new byte[8192];
                for (int k = 0; k < b.Size; ++k) {
                    int start = b.GetAsNumber(k).IntValue;
                    int length = b.GetAsNumber(++k).IntValue;
                    rf.Seek(start);
                    while (length > 0) {
                        int rd = rf.Read(buf, 0, Math.Min(length, buf.Length));
                        if (rd <= 0)
                            break;
                        length -= rd;
                        pkcs7.Update(buf, 0, rd);
                    }
                }
            }
            finally {
                try{rf.Close();}catch{}
            }
        }

        /**
        * Gets the total number of revisions this document has.
        * @return the total number of revisions
        */
        public int TotalRevisions {
            get {
                FindSignatureNames();
                return this.totalRevisions;
            }
        }
        
        /**
        * Gets this <CODE>field</CODE> revision.
        * @param field the signature field name
        * @return the revision or zero if it's not a signature field
        */    
        public int GetRevision(String field) {
            FindSignatureNames();
            field = GetTranslatedFieldName(field);
            if (!sigNames.ContainsKey(field))
                return 0;
            return ((int[])sigNames[field])[1];
        }
        
        /**
        * Extracts a revision from the document.
        * @param field the signature field name
        * @return an <CODE>Stream</CODE> covering the revision. Returns <CODE>null</CODE> if
        * it's not a signature field
        * @throws IOException on error
        */    
        public Stream ExtractRevision(String field) {
            FindSignatureNames();
            field = GetTranslatedFieldName(field);
            if (!sigNames.ContainsKey(field))
                return null;
            int length = ((int[])sigNames[field])[0];
            RandomAccessFileOrArray raf = reader.SafeFile;
            raf.ReOpen();
            raf.Seek(0);
            return new RevisionStream(raf, length);
        }

        /**
        * Sets a cache for field appearances. Parsing the existing PDF to
        * create a new TextField is time expensive. For those tasks that repeatedly
        * fill the same PDF with different field values the use of the cache has dramatic
        * speed advantages. An example usage:
        * <p>
        * <pre>
        * String pdfFile = ...;// the pdf file used as template
        * ArrayList xfdfFiles = ...;// the xfdf file names
        * ArrayList pdfOutFiles = ...;// the output file names, one for each element in xpdfFiles
        * Hashtable cache = new Hashtable();// the appearances cache
        * PdfReader originalReader = new PdfReader(pdfFile);
        * for (int k = 0; k &lt; xfdfFiles.Size(); ++k) {
        *    PdfReader reader = new PdfReader(originalReader);
        *    XfdfReader xfdf = new XfdfReader((String)xfdfFiles.Get(k));
        *    PdfStamper stp = new PdfStamper(reader, new FileOutputStream((String)pdfOutFiles.Get(k)));
        *    AcroFields af = stp.GetAcroFields();
        *    af.SetFieldCache(cache);
        *    af.SetFields(xfdf);
        *    stp.Close();
        * }
        * </pre>
        * @param fieldCache an HasMap that will carry the cached appearances
        */
        public Hashtable FieldCache {
            set {
                fieldCache = value;
            }
            get {
                return fieldCache;
            }
        }
        
        private void MarkUsed(PdfObject obj) {
            if (!append)
                return;
            ((PdfStamperImp)writer).MarkUsed(obj);
        }

        /**
        * Sets extra margins in text fields to better mimic the Acrobat layout.
        * @param extraMarginLeft the extra marging left
        * @param extraMarginTop the extra margin top
        */    
        public void SetExtraMargin(float extraMarginLeft, float extraMarginTop) {
            this.extraMarginLeft = extraMarginLeft;
            this.extraMarginTop = extraMarginTop;
        }

        /**
        * Adds a substitution font to the list. The fonts in this list will be used if the original
        * font doesn't contain the needed glyphs.
        * @param font the font
        */
        public void AddSubstitutionFont(BaseFont font) {
            if (substitutionFonts == null)
                substitutionFonts = new ArrayList();
            substitutionFonts.Add(font);
        }

        private static Hashtable stdFieldFontNames = new Hashtable();
        
        /**
        * Holds value of property fieldCache.
        */
        private Hashtable fieldCache;

        private int totalRevisions;

        static AcroFields() {
            stdFieldFontNames["CoBO"] = new String[]{"Courier-BoldOblique"};
            stdFieldFontNames["CoBo"] = new String[]{"Courier-Bold"};
            stdFieldFontNames["CoOb"] = new String[]{"Courier-Oblique"};
            stdFieldFontNames["Cour"] = new String[]{"Courier"};
            stdFieldFontNames["HeBO"] = new String[]{"Helvetica-BoldOblique"};
            stdFieldFontNames["HeBo"] = new String[]{"Helvetica-Bold"};
            stdFieldFontNames["HeOb"] = new String[]{"Helvetica-Oblique"};
            stdFieldFontNames["Helv"] = new String[]{"Helvetica"};
            stdFieldFontNames["Symb"] = new String[]{"Symbol"};
            stdFieldFontNames["TiBI"] = new String[]{"Times-BoldItalic"};
            stdFieldFontNames["TiBo"] = new String[]{"Times-Bold"};
            stdFieldFontNames["TiIt"] = new String[]{"Times-Italic"};
            stdFieldFontNames["TiRo"] = new String[]{"Times-Roman"};
            stdFieldFontNames["ZaDb"] = new String[]{"ZapfDingbats"};
            stdFieldFontNames["HySm"] = new String[]{"HYSMyeongJo-Medium", "UniKS-UCS2-H"};
            stdFieldFontNames["HyGo"] = new String[]{"HYGoThic-Medium", "UniKS-UCS2-H"};
            stdFieldFontNames["KaGo"] = new String[]{"HeiseiKakuGo-W5", "UniKS-UCS2-H"};
            stdFieldFontNames["KaMi"] = new String[]{"HeiseiMin-W3", "UniJIS-UCS2-H"};
            stdFieldFontNames["MHei"] = new String[]{"MHei-Medium", "UniCNS-UCS2-H"};
            stdFieldFontNames["MSun"] = new String[]{"MSung-Light", "UniCNS-UCS2-H"};
            stdFieldFontNames["STSo"] = new String[]{"STSong-Light", "UniGB-UCS2-H"};
        }

        public class RevisionStream : Stream {
            private byte[] b = new byte[1];
            private RandomAccessFileOrArray raf;
            private int length;
            private int rangePosition = 0;
            private bool closed;
            
            internal RevisionStream(RandomAccessFileOrArray raf, int length) {
                this.raf = raf;
                this.length = length;
            }
            
            public override int ReadByte() {
                int n = Read(b, 0, 1);
                if (n != 1)
                    return -1;
                return b[0] & 0xff;
            }
            
            public override int Read(byte[] b, int off, int len) {
                if (b == null) {
                    throw new ArgumentNullException();
                } else if ((off < 0) || (off > b.Length) || (len < 0) ||
                ((off + len) > b.Length) || ((off + len) < 0)) {
                    throw new ArgumentOutOfRangeException();
                } else if (len == 0) {
                    return 0;
                }
                if (rangePosition >= length) {
                    Close();
                    return -1;
                }
                int elen = Math.Min(len, length - rangePosition);
                raf.ReadFully(b, off, elen);
                rangePosition += elen;
                return elen;
            }
            
            public override void Close() {
                if (!closed) {
                    raf.Close();
                    closed = true;
                }
            }
        
            public override bool CanRead {
                get {
                    return true;
                }
            }
        
            public override bool CanSeek {
                get {
                    return false;
                }
            }
        
            public override bool CanWrite {
                get {
                    return false;
                }
            }
        
            public override long Length {
                get {
                    return 0;
                }
            }
        
            public override long Position {
                get {
                    return 0;
                }
                set {
                }
            }
        
            public override void Flush() {
            }
        
            public override long Seek(long offset, SeekOrigin origin) {
                return 0;
            }
        
            public override void SetLength(long value) {
            }
        
            public override void Write(byte[] buffer, int offset, int count) {
            }
        }
        
        private class ISorterComparator : IComparer {        
            public int Compare(Object o1, Object o2) {
                int n1 = ((int[])((Object[])o1)[1])[0];
                int n2 = ((int[])((Object[])o2)[1])[0];
                return n1 - n2;
            }        
        }

        /**
        * Sets a list of substitution fonts. The list is composed of <CODE>BaseFont</CODE> and can also be <CODE>null</CODE>. The fonts in this list will be used if the original
        * font doesn't contain the needed glyphs.
        * @param substitutionFonts the list
        */
        public ArrayList SubstitutionFonts {
            set {
                substitutionFonts = value;
            }
            get {
                return substitutionFonts;
            }
        }

        /**
        * Gets the XFA form processor.
        * @return the XFA form processor
        */
        public XfaForm Xfa {
            get {
                return xfa;
            }
        }

        private static readonly PdfName[] buttonRemove = {PdfName.MK, PdfName.F , PdfName.FF , PdfName.Q , PdfName.BS , PdfName.BORDER};
        
        /**
        * Creates a new pushbutton from an existing field. If there are several pushbuttons with the same name
        * only the first one is used. This pushbutton can be changed and be used to replace 
        * an existing one, with the same name or other name, as long is it is in the same document. To replace an existing pushbutton
        * call {@link #replacePushbuttonField(String,PdfFormField)}.
        * @param field the field name that should be a pushbutton
        * @return a new pushbutton or <CODE>null</CODE> if the field is not a pushbutton
        */
        public PushbuttonField GetNewPushbuttonFromField(String field) {
            return GetNewPushbuttonFromField(field, 0);
        }

        /**
        * Creates a new pushbutton from an existing field. This pushbutton can be changed and be used to replace 
        * an existing one, with the same name or other name, as long is it is in the same document. To replace an existing pushbutton
        * call {@link #replacePushbuttonField(String,PdfFormField,int)}.
        * @param field the field name that should be a pushbutton
        * @param order the field order in fields with same name
        * @return a new pushbutton or <CODE>null</CODE> if the field is not a pushbutton
        */
        public PushbuttonField GetNewPushbuttonFromField(String field, int order) {
            if (GetFieldType(field) != FIELD_TYPE_PUSHBUTTON)
                return null;
            Item item = GetFieldItem(field);
            if (order >= item.Size)
                return null;
            int posi = order * 5;
            float[] pos = GetFieldPositions(field);
            Rectangle box = new Rectangle(pos[posi + 1], pos[posi + 2], pos[posi + 3], pos[posi + 4]);
            PushbuttonField newButton = new PushbuttonField(writer, box, null);
            PdfDictionary dic = item.GetMerged(order);
            DecodeGenericDictionary(dic, newButton);
            PdfDictionary mk = dic.GetAsDict(PdfName.MK);
            if (mk != null) {
                PdfString text = mk.GetAsString(PdfName.CA);
                if (text != null)
                    newButton.Text = text.ToUnicodeString();
                PdfNumber tp = mk.GetAsNumber(PdfName.TP);
                if (tp != null)
                    newButton.Layout = tp.IntValue + 1;
                PdfDictionary ifit = mk.GetAsDict(PdfName.IF);
                if (ifit != null) {
                    PdfName sw = ifit.GetAsName(PdfName.SW);
                    if (sw != null) {
                        int scale = PushbuttonField.SCALE_ICON_ALWAYS;
                        if (sw.Equals(PdfName.B))
                            scale = PushbuttonField.SCALE_ICON_IS_TOO_BIG;
                        else if (sw.Equals(PdfName.S))
                            scale = PushbuttonField.SCALE_ICON_IS_TOO_SMALL;
                        else if (sw.Equals(PdfName.N))
                            scale = PushbuttonField.SCALE_ICON_NEVER;
                        newButton.ScaleIcon = scale;
                    }
                    sw = ifit.GetAsName(PdfName.S);
                    if (sw != null) {
                        if (sw.Equals(PdfName.A))
                            newButton.ProportionalIcon = false;
                    }
                    PdfArray aj = ifit.GetAsArray(PdfName.A);
                    if (aj != null && aj.Size == 2) {
                        float left = aj.GetAsNumber(0).FloatValue;
                        float bottom = aj.GetAsNumber(1).FloatValue;
                        newButton.IconHorizontalAdjustment = left;
                        newButton.IconVerticalAdjustment = bottom;
                    }
                    PdfBoolean fb = ifit.GetAsBoolean(PdfName.FB);
                    if (fb != null && fb.BooleanValue)
                        newButton.IconFitToBounds = true;
                }
                PdfObject i = mk.Get(PdfName.I);
                if (i != null && i.IsIndirect())
                    newButton.IconReference = (PRIndirectReference)i;
            }
            return newButton;
        }
        
        /**
        * Replaces the first field with a new pushbutton. The pushbutton can be created with
        * {@link #getNewPushbuttonFromField(String)} from the same document or it can be a
        * generic PdfFormField of the type pushbutton.
        * @param field the field name
        * @param button the <CODE>PdfFormField</CODE> representing the pushbutton
        * @return <CODE>true</CODE> if the field was replaced, <CODE>false</CODE> if the field
        * was not a pushbutton
        */
        public bool ReplacePushbuttonField(String field, PdfFormField button) {
            return ReplacePushbuttonField(field, button, 0);
        }
        
        /**
        * Replaces the designated field with a new pushbutton. The pushbutton can be created with
        * {@link #getNewPushbuttonFromField(String,int)} from the same document or it can be a
        * generic PdfFormField of the type pushbutton.
        * @param field the field name
        * @param button the <CODE>PdfFormField</CODE> representing the pushbutton
        * @param order the field order in fields with same name
        * @return <CODE>true</CODE> if the field was replaced, <CODE>false</CODE> if the field
        * was not a pushbutton
        */
        public bool ReplacePushbuttonField(String field, PdfFormField button, int order) {
            if (GetFieldType(field) != FIELD_TYPE_PUSHBUTTON)
                return false;
            Item item = GetFieldItem(field);
            if (order >= item.Size)
                return false;
            PdfDictionary merged = item.GetMerged(order);
            PdfDictionary values = item.GetValue(order);
            PdfDictionary widgets = item.GetWidget(order);
            for (int k = 0; k < buttonRemove.Length; ++k) {
                merged.Remove(buttonRemove[k]);
                values.Remove(buttonRemove[k]);
                widgets.Remove(buttonRemove[k]);
            }
            foreach (PdfName key in button.Keys) {
                if (key.Equals(PdfName.T) || key.Equals(PdfName.RECT))
                    continue;
                if (key.Equals(PdfName.FF))
                    values.Put(key, button.Get(key));
                else
                    widgets.Put(key, button.Get(key));
                merged.Put(key, button.Get(key));
            }
            return true;
        }
    }
}
