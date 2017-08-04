﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kuriimu.Compression;
using Kuriimu.Kontract;
using Kuriimu.IO;
using System;
using Cetera.Hash;

namespace archive_level5.XFSA
{
    public sealed class XFSA
    {
        public List<XFSAFileInfo> Files = new List<XFSAFileInfo>();
        Stream _stream = null;

        Header header;
        byte[] table1;
        byte[] table2;
        byte[] nameC;
        List<FileEntry> entries = new List<FileEntry>();
        List<string> fileNames = new List<string>();

        public XFSA(string filename)
        {
            using (var br = new BinaryReaderX(File.OpenRead(filename), true))
            {
                //Header
                header = br.ReadStruct<Header>();

                //1st table
                br.BaseStream.Position = header.offset1;
                table1 = br.ReadBytes((int)(header.offset2 - header.offset1));

                //2nd table
                br.BaseStream.Position = header.offset2;
                table2 = br.ReadBytes((int)(header.fileEntryTableOffset - header.offset2));

                //File Entry Table
                br.BaseStream.Position = header.fileEntryTableOffset;
                entries = new BinaryReaderX(new MemoryStream(Level5.Decompress(new MemoryStream(br.ReadBytes((int)(header.nameTableOffset - header.fileEntryTableOffset))))))
                    .ReadMultiple<FileEntry>((int)header.fileEntryCount);

                //Name Table
                br.BaseStream.Position = header.nameTableOffset;
                nameC = br.ReadBytes((int)(header.dataOffset - header.nameTableOffset));
                fileNames = GetFileNames(Level5.Decompress(new MemoryStream(nameC)));

                //Add Files
                var count = 0;
                foreach (var name in fileNames)
                {
                    var crc32 = Crc32.Create(name.Split('/').Last());
                    var entry = entries.Find(c => c.crc32 == crc32);
                    Files.Add(new XFSAFileInfo
                    {
                        State = ArchiveFileState.Archived,
                        FileName = name,
                        FileData = new SubStream(br.BaseStream, header.dataOffset + ((entry.comb1 & 0x00ffffff) << 4), entry.comb2 & 0x000fffff),
                        crc32 = crc32
                    });
                }
            }
        }

        public List<string> GetFileNames(byte[] namePart)
        {
            List<string> names = new List<string>();

            using (var br = new BinaryReaderX(new MemoryStream(namePart)))
            {
                string currentDir = "";
                br.BaseStream.Position = 1;

                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    string tmpString = br.ReadCStringSJIS();
                    if (tmpString[tmpString.Length - 1] == '/')
                    {
                        currentDir = tmpString;
                    }
                    else
                    {
                        names.Add(currentDir + tmpString);
                    }
                }
            }

            return names;
        }

        public void Save(Stream xfsa)
        {
            using (BinaryWriterX bw = new BinaryWriterX(xfsa))
            {
                int dataOffset = ((0x24 + table1.Length + table2.Length + nameC.Length + entries.Count * 0xc + 4) + 0xf) & ~0xf;

                //Table 1 & 2
                bw.BaseStream.Position = 0x24;
                bw.Write(table1);
                bw.Write(table2);

                //FileEntries Table
                bw.Write((entries.Count * 0xc) << 3);
                var files = Files.OrderBy(c => c.crc32);

                uint offset = 0;
                int count = 0;
                foreach (var entry in entries)
                {
                    //catch file limits
                    if (Files[count].FileData.Length >= 0x100000)
                    {
                        throw new Exception("File " + Files[count].FileName + " is too big to pack into this archive type!");
                    }
                    else if (offset + dataOffset >= 0x10000000)
                    {
                        throw new Exception("The archive can't be bigger than 0x10000000 Bytes.");
                    }

                    //edit entry
                    entry.comb1 = (entry.comb1 & 0xff000000) | (offset >> 4);
                    entry.comb2 = (entry.comb1 & 0xfff00000) | ((uint)Files[count].FileData.Length);

                    //write entry
                    bw.WriteStruct(entry);

                    //edit values
                    offset = (uint)(((offset + Files[count].FileData.Length) + 0xf) & ~0xf);
                    count++;
                }

                //Nametable
                bw.Write(nameC);

                //Files
                bw.BaseStream.Position = dataOffset;
                files = Files.OrderBy(c => c.FileData.Position);
                foreach (var file in files)
                {
                    file.FileData.CopyTo(bw.BaseStream);
                    bw.BaseStream.Position = (bw.BaseStream.Position + 0xf) & ~0xf;
                }

                //Header
                header.nameTableOffset = (uint)(0x24 + table1.Length + table2.Length + entries.Count * 0xc + 4);
                header.dataOffset = (uint)dataOffset;
                bw.BaseStream.Position = 0;
                bw.WriteStruct(header);
            }
        }

        public void Close()
        {
            _stream?.Close();
            _stream = null;
        }
    }
}
