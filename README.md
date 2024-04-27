# RawGray

Extract Bayer channels from RAW images

![Screenshot](docs/screen.png)

# How to use it

- Convert RAW images to 16 bit TIFF. You can use [RawTherapee](https://www.rawtherapee.com) by setting the Demosaicing Method "None" or [LibRaw](https://www.libraw.org/) with the following command "dcraw_emu.exe -T -4 -disinterp -o 0 input.dng"
- Open the TIFF image (Grey/16 pixel format) with RawGray
- By using the R,G1,G2,B sliders, balance as desired
- Export Channels to write 4 images, one for each channel

# Notes

- RAW images are supposed to have the bayer pattern RGGB (ex. Sony Alpha Camera). Different patterns require code changes or just rename the exported files (ex. for BGGR swap R and B channels).
- There are some TIFF sample images bundled with the software. Try them to on-board quickly.

# Requirements

[.NET 6.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
