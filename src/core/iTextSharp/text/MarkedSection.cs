using System;
using System.Collections.Generic;
using System.Text;
using System.util;
/*
 * $Id: MarkedSection.cs,v 1.7 2008/05/13 11:25:12 psoares33 Exp $
 * 
 *
 * Copyright 2007 by Bruno Lowagie.
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
 * the Initial Developer are Copyright (C) 1999-2007 by Bruno Lowagie.
 * All Rights Reserved.
 * Co-Developer of the code is Paulo Soares. Portions created by the Co-Developer
 * are Copyright (C) 2000-2007 by Paulo Soares. All Rights Reserved.
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

namespace iTextSharp.text {

    /**
    * Wrapper that allows to add properties to a Chapter/Section object.
    * Before iText 1.5 every 'basic building block' implemented the MarkupAttributes interface.
    * By setting attributes, you could add markup to the corresponding XML and/or HTML tag.
    * This functionality was hardly used by anyone, so it was removed, and replaced by
    * the MarkedObject functionality.
    */

    public class MarkedSection : MarkedObject {

        /** This is the title of this section. */
        protected MarkedObject title = null;
            
        /**
        * Creates a MarkedObject with a Section or Chapter object.
        * @param section   the marked section
        */
        public MarkedSection(Section section) : base() {
            if (section.Title != null) {
                title = new MarkedObject(section.Title);
                section.Title = null;
            }
            this.element = section;
        }
        
        /**
        * Adds a <CODE>Paragraph</CODE>, <CODE>List</CODE> or <CODE>Table</CODE>
        * to this <CODE>Section</CODE>.
        *
        * @param   index   index at which the specified element is to be inserted
        * @param   o       an object of type <CODE>Paragraph</CODE>, <CODE>List</CODE> or <CODE>Table</CODE>=
        * @throws  ClassCastException if the object is not a <CODE>Paragraph</CODE>, <CODE>List</CODE> or <CODE>Table</CODE>
        */
         
        public void Add(int index, IElement o) {
            ((Section)element).Add(index, o);
        }
            
        /**
        * Adds a <CODE>Paragraph</CODE>, <CODE>List</CODE>, <CODE>Table</CODE> or another <CODE>Section</CODE>
        * to this <CODE>Section</CODE>.
        *
        * @param   o       an object of type <CODE>Paragraph</CODE>, <CODE>List</CODE>, <CODE>Table</CODE> or another <CODE>Section</CODE>
        * @return  a bool
        * @throws  ClassCastException if the object is not a <CODE>Paragraph</CODE>, <CODE>List</CODE>, <CODE>Table</CODE> or <CODE>Section</CODE>
        */
            
        public bool Add(IElement o) {
            return ((Section)element).Add(o);
        }

        /**
        * Processes the element by adding it (or the different parts) to an
        * <CODE>ElementListener</CODE>.
        *
        * @param       listener        an <CODE>ElementListener</CODE>
        * @return <CODE>true</CODE> if the element was processed successfully
        */
        public override bool Process(IElementListener listener) {
            try {
                foreach (IElement element in ((Section)this.element)) {
                    listener.Add(element);
                }
                return true;
            }
            catch (DocumentException) {
                return false;
            }
        }
        
        /**
        * Adds a collection of <CODE>Element</CODE>s
        * to this <CODE>Section</CODE>.
        *
        * @param   collection  a collection of <CODE>Paragraph</CODE>s, <CODE>List</CODE>s and/or <CODE>Table</CODE>s
        * @return  <CODE>true</CODE> if the action succeeded, <CODE>false</CODE> if not.
        * @throws  ClassCastException if one of the objects isn't a <CODE>Paragraph</CODE>, <CODE>List</CODE>, <CODE>Table</CODE>
        */
            
        public bool AddAll(IEnumerable<IElement> collection) {
            return ((Section)element).AddAll(collection);
        }
            
        /**
        * Creates a <CODE>Section</CODE>, adds it to this <CODE>Section</CODE> and returns it.
        *
        * @param   indentation the indentation of the new section
        * @param   numberDepth the numberDepth of the section
        * @return  a new Section object
        */
            
        public MarkedSection AddSection(float indentation, int numberDepth) {
            MarkedSection section = ((Section)element).AddMarkedSection();
            section.Indentation = indentation;
            section.NumberDepth = numberDepth;
            return section;
        }
            
        /**
        * Creates a <CODE>Section</CODE>, adds it to this <CODE>Section</CODE> and returns it.
        *
        * @param   indentation the indentation of the new section
        * @return  a new Section object
        */
            
        public MarkedSection AddSection(float indentation) {
            MarkedSection section = ((Section)element).AddMarkedSection();
            section.Indentation = indentation;
            return section;
        }
            
        /**
        * Creates a <CODE>Section</CODE>, add it to this <CODE>Section</CODE> and returns it.
        *
        * @param   numberDepth the numberDepth of the section
        * @return  a new Section object
        */
        public MarkedSection AddSection(int numberDepth) {
            MarkedSection section = ((Section)element).AddMarkedSection();
            section.NumberDepth = numberDepth;
            return section;
        }
            
        /**
        * Creates a <CODE>Section</CODE>, adds it to this <CODE>Section</CODE> and returns it.
        *
        * @return  a new Section object
        */
        public MarkedSection AddSection() {
            return ((Section)element).AddMarkedSection();
        }
            
        // public methods
            
        /**
        * Sets the title of this section.
        *
        * @param   title   the new title
        */
        public MarkedObject Title {
            set {
                if (value.element is Paragraph)
                    this.title = value;
            }
            get {
                Paragraph result = Section.ConstructTitle((Paragraph)title.element, ((Section)element).numbers, ((Section)element).NumberDepth, ((Section)element).NumberStyle);
                MarkedObject mo = new MarkedObject(result);
                mo.markupAttributes = title.MarkupAttributes;
                return mo;
            }
        }
           
        /**
        * Sets the depth of the sectionnumbers that will be shown preceding the title.
        * <P>
        * If the numberdepth is 0, the sections will not be numbered. If the numberdepth
        * is 1, the section will be numbered with their own number. If the numberdepth is
        * higher (for instance x > 1), the numbers of x - 1 parents will be shown.
        *
        * @param   numberDepth     the new numberDepth
        */
        public int NumberDepth {
            set {
                ((Section)element).NumberDepth = value;
            }
        }
            
        /**
        * Sets the indentation of this <CODE>Section</CODE> on the left side.
        *
        * @param   indentation     the indentation
        */
        public float IndentationLeft {
            set {
                ((Section)element).IndentationLeft = value;
            }
        }
            
        /**
        * Sets the indentation of this <CODE>Section</CODE> on the right side.
        *
        * @param   indentation     the indentation
        */
            
        public float IndentationRight {
            set {
                ((Section)element).IndentationRight = value;
            }
        }
            
        /**
        * Sets the indentation of the content of this <CODE>Section</CODE>.
        *
        * @param   indentation     the indentation
        */
        public float Indentation {
            set {
                ((Section)element).Indentation = value;
            }
        }
            
        /** Setter for property bookmarkOpen.
        * @param bookmarkOpen false if the bookmark children are not
        * visible.
        */
        public bool BookmarkOpen {
            set {
                ((Section)element).BookmarkOpen = value;
            }
        }
            
        /**
        * Setter for property triggerNewPage.
        * @param triggerNewPage true if a new page has to be triggered.
        */
        public bool TriggerNewPage {
            set {
                ((Section)element).TriggerNewPage = value;
            }
        }
            
        /**
        * Sets the bookmark title. The bookmark title is the same as the section title but
        * can be changed with this method.
        * @param bookmarkTitle the bookmark title
        */    
        public String BookmarkTitle {
            set {
                ((Section)element).BookmarkTitle = value;
            }
        }

        /**
        * Adds a new page to the section.
        * @since    2.1.1
        */
        public void NewPage() {
            ((Section)element).NewPage();
        }
    }
}