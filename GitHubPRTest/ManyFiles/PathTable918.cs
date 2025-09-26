//************************************************************************************************
// PathTable918.cs
//
// Copyright (c) Microsoft Corporation
//************************************************************************************************
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace GitHubPRTest
{
    internal enum PathTableRecursion918
    {
        Full = 2,
        None = 0,
        OneLevel = 1
    }

    internal struct PathTableTokenAndValue918<T>
    {
        public PathTableTokenAndValue918(String token, T value)
        {
            this.Token = token;
            this.Value = value;
        }

        public readonly String Token;
        public readonly T Value;
    }

    internal class PathTable918<T>
    {
        public PathTable918(char tokenSeparator, bool caseInsensitive)
        {
            m_tokenSeparator = tokenSeparator;
            m_tokenSeparatorString = new String(tokenSeparator, 1);
            m_list = new List<PathTableRow<T>>();
            m_fullPathCompare = new EntryCompare(FullPathCompare);
            m_parentPathCompare = new EntryCompare(ParentPathCompare);
            m_sorted = true;

            if (caseInsensitive)
            {
                m_comparison = StringComparison.OrdinalIgnoreCase;
            }
            else
            {
                m_comparison = StringComparison.Ordinal;
            }
        }

        public void Reserve(int capacity)
        {
            if (capacity > m_list.Capacity)
            {
                m_list.Capacity = capacity;
            }
        }

        public void Add(String token, T referencedObject, bool overwrite = false)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            RequireSorted();

            token = CanonicalizeToken(token);

            int parentPathLength = GetParentPathLength(token);
            int index = Seek(m_fullPathCompare, token, parentPathLength, token.Length);

            if (index < 0)
            {
                m_list.Insert(~index, new PathTableRow<T>(token, parentPathLength, 0, referencedObject));
            }
            else
            {
                if (!overwrite)
                {
                    throw new ArgumentException("The token already exists in the PathTable918.", "token");
                }

                m_list[index] = new PathTableRow<T>(token, parentPathLength, 0, referencedObject);
            }
        }

        public void ModifyInPlace(String token, ModifyInPlaceCallback callback)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            RequireSorted();

            token = CanonicalizeToken(token);

            int parentPathLength = GetParentPathLength(token);
            int index = Seek(m_fullPathCompare, token, parentPathLength, token.Length);

            T referencedObject;
            PathTableRow<T> row;

            if (index >= 0)
            {
                row = m_list[index];
                referencedObject = row.ReferencedObject;
            }
            else
            {
                row = default(PathTableRow<T>);
                referencedObject = default(T);
            }

            referencedObject = callback(referencedObject);

            if (index >= 0)
            {
                m_list[index] = new PathTableRow<T>(row.Token, row.ParentPathLength, row.OriginalIndex, referencedObject);
            }
            else
            {
                m_list.Insert(~index, new PathTableRow<T>(token, parentPathLength, 0, referencedObject));
            }
        }

        public void AddUnsorted(String token, T referencedObject)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token = CanonicalizeToken(token);

            int parentPathLength = GetParentPathLength(token);

            if (m_sorted &&
                m_list.Count > 0 &&
                // Make sure to unset the sorted bit when the comparison returns zero
                FullPathCompare(m_list[m_list.Count - 1], token, parentPathLength, token.Length) >= 0)
            {
                m_sorted = false;
            }

            m_list.Add(new PathTableRow<T>(token, parentPathLength, m_list.Count, referencedObject));
        }

        public void Sort(bool checkForDuplicates = false)
        {
            Func<String, T, T, bool> duplicateHandler;

            if (checkForDuplicates)
            {
                duplicateHandler = DefaultDuplicateHandler;
            }
            else
            {
                duplicateHandler = null;
            }

            Sort(duplicateHandler);
        }

        private static bool DefaultDuplicateHandler(String token, T value1, T value2)
        {
            // Indicate that the sort routine should throw.
            return false;
        }

        public void Sort(Func<String, T, T, bool> duplicateHandler)
        {
            if (!m_sorted)
            {
                m_list.Sort(delegate (PathTableRow<T> a, PathTableRow<T> b)
                {
                    int toReturn = FullPathCompare(a, b.Token, b.ParentPathLength, b.Token.Length);

                    // This isn't a stable sort, so we persist the original index and stabilize
                    // here. On x64, the memory is probably free. Each PathTableRow has a qword
                    // pointer for Token and a dword for ParentPathLength, so we have an extra dword
                    // (now used for OriginalIndex) to get back to qword alignment for whatever T is
                    //
                    // AddUnsorted persists the insertion index when it adds new rows. Other methods
                    // which do insertions into the sorted list persist zero
                    if (0 == toReturn)
                    {
                        toReturn = a.OriginalIndex - b.OriginalIndex;
                    }

                    return toReturn;
                });

                if (null != duplicateHandler)
                {
                    for (int i = m_list.Count - 1; i > 0; i--)
                    {
                        if (0 == FullPathCompare(m_list[i - 1], m_list[i].Token, m_list[i].ParentPathLength, m_list[i].Token.Length))
                        {
                            if (!duplicateHandler(m_list[i - 1].Token, m_list[i - 1].ReferencedObject, m_list[i].ReferencedObject))
                            {
                                throw new ArgumentException("Duplicate tokens exist in the PathTable918.");
                            }
                            else
                            {
                                // The handler asked us to resolve this collision with a last-writer wins policy.
                                m_list.RemoveAt(i - 1);
                            }
                        }
                    }
                }

                m_sorted = true;
            }
        }

        public bool TryGetByIndex(int index, out T referencedObject)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RequireSorted();

            if (index < m_list.Count)
            {
                referencedObject = m_list[index].ReferencedObject;

                return true;
            }
            else
            {
                referencedObject = default(T);

                return false;
            }
        }

        public bool TryGetValue(String token, out T referencedObject)
        {
            return TryGetValueAndIndex(token, out referencedObject, out int _);
        }

        public bool TryGetValueAndIndex(String token, out T referencedObject, out int index)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            RequireSorted();

            token = CanonicalizeToken(token);

            index = Seek(m_fullPathCompare, token, GetParentPathLength(token), token.Length);

            if (index >= 0)
            {
                referencedObject = m_list[index].ReferencedObject;

                return true;
            }
            else
            {
                referencedObject = default(T);
                index = -1;

                return false;
            }
        }

        public T this[String token]
        {
            get
            {
                T referencedObject;

                if (!TryGetValue(token, out referencedObject))
                {
                    throw new KeyNotFoundException();
                }

                return referencedObject;
            }

            set
            {
                Add(token, value, overwrite: true);
            }
        }

        public void SetValueAtIndex(int index, String token, T value)
        {
            if (index < 0 || index > (m_list.Count - 1))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            RequireSorted();

            token = CanonicalizeToken(token);

            PathTableRow<T> existingValue = m_list[index];

            if (!String.Equals(token, existingValue.Token, m_comparison))
            {
                throw new ArgumentOutOfRangeException("token");
            }

            m_list[index] = new PathTableRow<T>(token, existingValue.ParentPathLength, existingValue.OriginalIndex, value);
        }

        public int Count
        {
            get
            {
                return m_list.Count;
            }
        }

        public void Clear()
        {
            m_list.Clear();
            m_sorted = true;
        }

        public bool Remove(String token, bool removeChildren)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            RequireSorted();

            token = CanonicalizeToken(token);

            bool removed = false;

            if (removeChildren)
            {
                String parentPath = String.Concat(token, m_tokenSeparatorString);
                int startIndex = ~Seek(m_parentPathCompare, parentPath, 1, 0);
                Debug.Assert(startIndex >= 0);

                if (startIndex < m_list.Count &&
                    IsSubItem(m_list[startIndex].Token, token))
                {
                    int endIndex = ~Seek(m_parentPathCompare, EndRange(parentPath), 1, 0, startIndex);
                    Debug.Assert(endIndex >= 0);

                    m_list.RemoveRange(startIndex, endIndex - startIndex);
                    removed = true;
                }
            }

            int index = Seek(m_fullPathCompare, token, GetParentPathLength(token), token.Length);

            if (index >= 0)
            {
                m_list.RemoveAt(index);
                removed = true;
            }

            return removed;
        }

        public IEnumerable<PathTableTokenAndValue918<T>> EnumRoots()
        {
            RequireSorted();

            PathTableRanges ranges = new PathTableRanges(new PathTableRange(0, m_list.Count));

            for (int i = 0; i < ranges.Ranges.Count; i++)
            {
                PathTableRange range = ranges.Ranges[i];

                for (int j = range.StartIndex; j < range.EndIndex; j++)
                {
                    PathTableRow<T> row = m_list[j];

                    yield return new PathTableTokenAndValue918<T>(row.Token, row.ReferencedObject);

                    PathTableRange children = RangeSeek(row.Token, PathTableRecursion918.Full, j, ranges.EndIndex);

                    if (children.Length > 0)
                    {
                        ranges.Exclude(children);

                        // The exclusion may have shortened our current range; re-fetch it
                        range = ranges.Ranges[i];
                    }
                }
            }
        }

        public IEnumerable<T> EnumRootsReferencedObjects()
        {
            foreach (PathTableTokenAndValue918<T> entry in EnumRoots())
            {
                yield return entry.Value;
            }
        }

        public IEnumerable<PathTableTokenAndValue918<T>> EnumParents(String token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            RequireSorted();

            token = CanonicalizeToken(token);

            int index = 2147483647;
            int parentPathLength = GetParentPathLength(token);
            int tokenLength = token.Length;

            while (tokenLength >= 0)
            {
                index = Seek(m_fullPathCompare, token, parentPathLength, tokenLength, endIndex: index);

                if (index >= 0)
                {
                    yield return new PathTableTokenAndValue918<T>(m_list[index].Token, m_list[index].ReferencedObject);
                }
                else
                {
                    index = ~index;
                }

                if (0 == index)
                {
                    // Any future parents would be before the first item in the list,
                    // therefore they don't exist.
                    break;
                }

                index = index - 1;
                tokenLength = parentPathLength - 1;
                parentPathLength = GetParentPathLength(token, parentPathLength);
            }
        }

        public IEnumerable<T> EnumParentsReferencedObjects(String token)
        {
            foreach (var entry in EnumParents(token))
            {
                yield return entry.Value;
            }
        }

        /// <summary>
        /// Enumerate all of the differences between the two path tables.
        /// </summary>
        public static IEnumerable<PathTableTokenAndValue918<T>> EnumAllDifferences(PathTable918<T> pt1, PathTable918<T> pt2)
        {
            // This algorithm requires sorted PathTables for the iterative walk to work correctly
            pt1?.RequireSorted();
            pt2?.RequireSorted();

            using (IEnumerator<PathTableRow<T>> iter1 = pt1?.m_list.GetEnumerator())
            using (IEnumerator<PathTableRow<T>> iter2 = pt2?.m_list.GetEnumerator())
            {
                EntryCompare comparer = pt1?.m_fullPathCompare ?? pt2?.m_fullPathCompare;

                bool iter1Valid = iter1?.MoveNext() ?? false;
                bool iter2Valid = iter2?.MoveNext() ?? false;

                PathTableRow<T> s1, s2;

                while (iter1Valid || iter2Valid)
                {
                    s1 = iter1Valid ? iter1.Current : default(PathTableRow<T>);
                    s2 = iter2Valid ? iter2.Current : default(PathTableRow<T>);

                    if (iter1Valid && iter2Valid)
                    {
                        // Compare the items for equality
                        Debug.Assert(comparer != null);
                        int c = comparer(s1, s2.Token, s2.ParentPathLength, s2.Token.Length);

                        // If the items match, skip this item and advance both iterators
                        if (c == 0)
                        {
                            iter1Valid = iter1Valid && MoveNextNonDuplicate(iter1, comparer);
                            iter2Valid = iter2Valid && MoveNextNonDuplicate(iter2, comparer);
                        }
                        else if (c < 0)
                        {
                            // The first item sorts lower so keep it and advance its iterator
                            yield return new PathTableTokenAndValue918<T>(s1.Token, s1.ReferencedObject);
                            iter1Valid = iter1Valid && MoveNextNonDuplicate(iter1, comparer);
                        }
                        else
                        {
                            // The second item sorts lower so keep it and advance its iterator
                            yield return new PathTableTokenAndValue918<T>(s2.Token, s2.ReferencedObject);
                            iter2Valid = iter2Valid && MoveNextNonDuplicate(iter2, comparer);
                        }
                    }
                    else if (iter1Valid)
                    {
                        // iter2 is finished, so return s1 and advance iter1
                        yield return new PathTableTokenAndValue918<T>(s1.Token, s1.ReferencedObject);
                        iter1Valid = iter1Valid && MoveNextNonDuplicate(iter1, comparer);
                    }
                    else if (iter2Valid)
                    {
                        // iter1 is finished, so return s2 and advance iter2
                        yield return new PathTableTokenAndValue918<T>(s2.Token, s2.ReferencedObject);
                        iter2Valid = iter2Valid && MoveNextNonDuplicate(iter2, comparer);
                    }
                }
            }
        }

        /// <summary>
        /// MoveNext on the iterator, skipping any duplicates.
        /// </summary>
        private static bool MoveNextNonDuplicate(IEnumerator<PathTableRow<T>> iter, EntryCompare comparer)
        {
            PathTableRow<T> r = iter.Current;

            do
            {
                if (!iter.MoveNext())
                {
                    return false;
                }
            } while (comparer(iter.Current, r.Token, r.ParentPathLength, r.Token.Length) == 0);

            return true;
        }

        public IEnumerable<PathTableTokenAndValue918<T>> EnumSubTree(String token,
                                                                  bool enumerateSubTreeRoot,
                                                                  PathTableRecursion918 depth,
                                                                  IEnumerable<String> exclusions = null)
        {
            if (null == token &&
                PathTableRecursion918.Full == depth)
            {
                // Special case: enumerate the entire table

                foreach (PathTableRow<T> row in m_list)
                {
                    yield return new PathTableTokenAndValue918<T>(row.Token, row.ReferencedObject);
                }

                yield break;
            }

            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            RequireSorted();

            token = CanonicalizeToken(token);

            if (null != exclusions && exclusions.Any())
            {
                exclusions = exclusions.Select(s => CanonicalizeToken(s));
            }

            PathTableRanges ranges = new PathTableRanges(SubTreeSeek(token, enumerateSubTreeRoot, depth));

            if (null != exclusions && exclusions.Any())
            {
                depth = DepthReduce(depth);

                foreach (String exclusion in exclusions)
                {
                    ranges.Exclude(SubTreeSeek(exclusion, true, depth, ranges.StartIndex, ranges.EndIndex));
                }
            }

            foreach (PathTableRange range in ranges.Ranges)
            {
                for (int i = range.StartIndex; i < range.EndIndex; i++)
                {
                    PathTableRow<T> row = m_list[i];

                    yield return new PathTableTokenAndValue918<T>(row.Token, row.ReferencedObject);
                }
            }
        }

        public IEnumerable<T> EnumSubTreeReferencedObjects(String token,
                                                           bool enumerateSubTreeRoot,
                                                           PathTableRecursion918 depth,
                                                           IEnumerable<String> exclusions = null)
        {
            foreach (PathTableTokenAndValue918<T> tokenAndValue in EnumSubTree(token, enumerateSubTreeRoot, depth, exclusions))
            {
                yield return tokenAndValue.Value;
            }
        }

        private IEnumerable<PathTableRange> SubTreeSeek(String token,
                                                        bool enumerateSubTreeRoot,
                                                        PathTableRecursion918 depth,
                                                        int startIndex = 0,
                                                        int endIndex = 2147483647)
        {
            if (enumerateSubTreeRoot)
            {
                int rootIndex = Seek(m_fullPathCompare, token, GetParentPathLength(token), token.Length, startIndex, endIndex);

                if (rootIndex >= 0)
                {
                    yield return new PathTableRange(rootIndex, rootIndex + 1);

                    startIndex = rootIndex;
                }
            }

            if (PathTableRecursion918.None != depth)
            {
                yield return RangeSeek(token, depth, startIndex, endIndex);
            }
        }

        private PathTableRange RangeSeek(String token,
                                         PathTableRecursion918 depth,
                                         int startIndex = 0,
                                         int endIndex = 2147483647)
        {
            Debug.Assert(depth != PathTableRecursion918.None);

            PathTableRange range = default(PathTableRange);

            Debug.Assert(token.Length == 0 ||
                         token[token.Length - 1] != m_tokenSeparator);

            String parentPath = String.Concat(token, m_tokenSeparatorString);

            // Seek for the start of the range
            range.StartIndex = ~Seek(m_parentPathCompare, parentPath, 1, 0, startIndex, endIndex);
            Debug.Assert(range.StartIndex >= 0);

            range.EndIndex = range.StartIndex;

            // End range seek varies by recursion type
            if (PathTableRecursion918.OneLevel == depth)
            {
                if (range.StartIndex < m_list.Count &&
                    m_list[range.StartIndex].ParentPathLength == parentPath.Length &&
                    IsSubItem(m_list[range.StartIndex].Token, token))
                {
                    range.EndIndex = ~Seek(m_parentPathCompare, parentPath, -1, 0, range.StartIndex, endIndex);
                    Debug.Assert(range.EndIndex >= 0);
                }
            }
            else /* if (PathTableRecursion918.Full == depth) */
            {
                if (range.StartIndex < m_list.Count &&
                    IsSubItem(m_list[range.StartIndex].Token, token))
                {
                    range.EndIndex = ~Seek(m_parentPathCompare, EndRange(parentPath), 1, 0, range.StartIndex, endIndex);
                    Debug.Assert(range.EndIndex >= 0);
                }
            }

            return range;
        }

        public bool HasSubItem(String token, int startIndex = 0)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            RequireSorted();

            token = CanonicalizeToken(token);

            int index = ~Seek(m_parentPathCompare, String.Concat(token, m_tokenSeparatorString), 1, 0, startIndex);
            Debug.Assert(startIndex >= 0);

            return index < m_list.Count && IsSubItem(m_list[index].Token, token);
        }

        private String CanonicalizeToken(String token)
        {
            int length = GetCanonicalTokenLength(token);

            if (length < token.Length)
            {
                return token.Substring(0, length);
            }

            return token;
        }

        private int GetCanonicalTokenLength(String token)
        {
            int length = token.Length;

            while (length > 0 &&
                   token[length - 1] == m_tokenSeparator)
            {
                length--;
            }

            return length;
        }

        private void RequireSorted()
        {
            if (!m_sorted)
            {
                throw new InvalidOperationException();
            }
        }

        private int GetParentPathLength(String token)
        {
            return token.LastIndexOf(m_tokenSeparator) + 1;
        }

        private int GetParentPathLength(String token, int tokenLength)
        {
            if (tokenLength < 2)
            {
                return 0;
            }

            return token.LastIndexOf(m_tokenSeparator, tokenLength - 2, tokenLength - 1) + 1;
        }

        private String EndRange(String token)
        {
            StringBuilder endRangeB = new StringBuilder(token);
            endRangeB[endRangeB.Length - 1] = (char)(m_tokenSeparator + 1);
            return endRangeB.ToString();
        }

        private static PathTableRecursion918 DepthReduce(PathTableRecursion918 depth)
        {
            // Reduce PathTableRecursion918.OneLevel to PathTableRecursion918.None.
            if (PathTableRecursion918.OneLevel == depth)
            {
                depth = PathTableRecursion918.None;
            }

            return depth;
        }

        private bool IsSubItem(String item, String parent)
        {
            if (item.StartsWith(parent, m_comparison))
            {
                // StartsWith returning true, with equal lengths, implies that
                // item and parent are equal. This is not expected for IsSubItem
                // checks, where the item comes from later in the list than parent.
                Debug.Assert(item.Length > parent.Length);

                return item[parent.Length] == m_tokenSeparator;
            }

            return false;
        }

        private int Seek(EntryCompare compare, String token, int compareParam, int compareParam2, int startIndex = 0, int endIndex = 2147483647)
        {
            int lo = startIndex;
            int hi = m_list.Count - 1;

            if (endIndex < hi)
            {
                hi = endIndex;
            }

            while (lo <= hi)
            {
                int mid = (hi - lo) / 2 + lo;

                int c = compare(m_list[mid], token, compareParam, compareParam2);

                if (c < 0)
                {
                    lo = mid + 1;
                }
                else if (c > 0)
                {
                    hi = mid - 1;
                }
                else
                {
                    return mid;
                }
            }

            return ~lo;
        }

        #region Comparers

        private int FullPathCompare(PathTableRow<T> entry, String token, int parentPathLength, int tokenLength)
        {
            int order = String.Compare(entry.Token, 0,
                                       token, 0,
                                       entry.ParentPathLength < parentPathLength ? entry.ParentPathLength : parentPathLength,
                                       m_comparison);

            if (0 != order)
            {
                return order;
            }

            order = entry.ParentPathLength - parentPathLength;

            if (0 != order)
            {
                return order;
            }

            int childItemLength = tokenLength - parentPathLength;

            order = String.Compare(entry.Token, parentPathLength,
                                   token, parentPathLength,
                                   entry.ChildItemLength < childItemLength ? entry.ChildItemLength : childItemLength,
                                   m_comparison);

            if (0 != order)
            {
                return order;
            }

            return entry.Token.Length - tokenLength;
        }

        private int ParentPathCompare(PathTableRow<T> entry, String token, int equalityResult, int dummy)
        {
            int order = String.Compare(entry.Token, 0,
                                       token, 0,
                                       entry.ParentPathLength < token.Length ? entry.ParentPathLength : token.Length,
                                       m_comparison);

            if (0 != order)
            {
                return order;
            }

            order = entry.ParentPathLength - token.Length;

            if (0 != order)
            {
                return order;
            }

            return equalityResult;
        }

        #endregion

        public delegate T ModifyInPlaceCallback(T referencedObject);

        private delegate int EntryCompare(PathTableRow<T> entry, String token, int compareParam, int compareParam2);

        private bool m_sorted;

        private readonly EntryCompare m_fullPathCompare;
        private readonly EntryCompare m_parentPathCompare;
        private readonly List<PathTableRow<T>> m_list;
        private readonly char m_tokenSeparator;
        private readonly String m_tokenSeparatorString;
        private readonly StringComparison m_comparison;

        [DebuggerDisplay("Token = {Token}")]
        private struct PathTableRow<X>
        {
            public PathTableRow(String token, int parentPathLength, int originalIndex, X referencedObject)
            {
                this.Token = token;
                this.ParentPathLength = parentPathLength;
                this.OriginalIndex = originalIndex;
                this.ReferencedObject = referencedObject;
            }

            public int ChildItemLength
            {
                get
                {
                    return Token.Length - ParentPathLength;
                }
            }

            public String Token;
            public int ParentPathLength;
            public int OriginalIndex;
            public X ReferencedObject;
        }

        private class PathTableRanges
        {
            public PathTableRanges(PathTableRange range)
            {
                m_ranges = new List<PathTableRange>();
                m_ranges.Add(range);
            }

            public PathTableRanges(IEnumerable<PathTableRange> ranges)
            {
                m_ranges = new List<PathTableRange>(ranges);
            }

            public IReadOnlyList<PathTableRange> Ranges
            {
                get
                {
                    return m_ranges;
                }
            }

            public int StartIndex
            {
                get
                {
                    int startIndex = 0;

                    if (m_ranges.Count > 0)
                    {
                        startIndex = m_ranges[0].StartIndex;
                    }

                    return startIndex;
                }
            }

            public int EndIndex
            {
                get
                {
                    int endIndex = 0;

                    if (m_ranges.Count > 0)
                    {
                        endIndex = m_ranges[m_ranges.Count - 1].EndIndex;
                    }

                    return endIndex;
                }
            }

            public void Exclude(IEnumerable<PathTableRange> toExclude)
            {
                foreach (PathTableRange range in toExclude)
                {
                    Exclude(range);
                }
            }

            public void Exclude(PathTableRange toExclude)
            {
                for (int i = m_ranges.Count - 1; i >= 0; i--)
                {
                    PathTableRange range1, range2;

                    m_ranges[i].Abjunction(toExclude, out range1, out range2);

                    if (range1.Length > 0)
                    {
                        if (range2.Length > 0)
                        {
                            m_ranges[i] = range1;
                            m_ranges.Insert(i + 1, range2);
                        }
                        else
                        {
                            m_ranges[i] = range1;
                        }
                    }
                    else
                    {
                        if (range2.Length > 0)
                        {
                            m_ranges[i] = range2;
                        }
                        else
                        {
                            m_ranges.RemoveAt(i);
                        }
                    }
                }
            }

            private List<PathTableRange> m_ranges;
        }

        private struct PathTableRange
        {
            public PathTableRange(int startIndex, int endIndex)
            {
                this.StartIndex = startIndex;
                this.EndIndex = endIndex;
            }

            public PathTableRange And(PathTableRange range)
            {
                return new PathTableRange(this.StartIndex > range.StartIndex ? this.StartIndex : range.StartIndex,
                                          this.EndIndex < range.EndIndex ? this.EndIndex : range.EndIndex);
            }

            public void Not(out PathTableRange range1, out PathTableRange range2)
            {
                range1.StartIndex = 0;
                range1.EndIndex = this.StartIndex;

                range2.StartIndex = this.EndIndex;
                range2.EndIndex = 2147483647;
            }

            public void Abjunction(PathTableRange range, out PathTableRange range1, out PathTableRange range2)
            {
                range.Not(out range1, out range2);

                range1 = this.And(range1);
                range2 = this.And(range2);
            }

            public int Length
            {
                get
                {
                    int len = EndIndex - StartIndex;

                    if (len < 0)
                    {
                        len = 0;
                    }

                    return len;
                }
            }

            public int StartIndex;
            public int EndIndex;
        }
    }
}
