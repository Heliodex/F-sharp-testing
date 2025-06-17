// Mercury Setup Deployer 3, now in Go
// goroutines make it run fast as FUCK

package main

import (
	"archive/tar"
	"bytes"
	"compress/gzip"
	"crypto/sha3"
	"encoding/base32"
	"encoding/binary"
	"fmt"
	"io"
	"os"
	"time"
)

const (
	name     = "Mercury"
	input    = "./staging"
	output   = "./setup"
	launcher = input + "/" + name + "PlayerLauncher.exe"
	ext      = ".tar.gz"
)

var encoding = base32.NewEncoding("0123456789abcdefghijklmnopqrstuv").WithPadding(base32.NoPadding)

func compressStagingDir(o *bytes.Buffer) (id string, err error) {
	gz, _ := gzip.NewWriterLevel(o, gzip.BestCompression)
	defer gz.Close()

	w := tar.NewWriter(gz)
	defer w.Close()

	if err = w.AddFS(os.DirFS(input)); err != nil {
		return
	}

	// current unix timestamp
	now := time.Now().Unix()

	// convert int64 to bytes
	nowbytes := make([]byte, 8)
	binary.BigEndian.PutUint64(nowbytes, uint64(now))

	// trim leading zeros
	for i, b := range nowbytes {
		if b != 0 {
			nowbytes = nowbytes[i:]
			break
		}
	}

	enctime := encoding.EncodeToString(nowbytes)

	hash := sha3.SumSHAKE256(o.Bytes(), 4)
	enchash := encoding.EncodeToString(hash[:])

	return enctime + "-" + enchash, nil
}

func writeStagingDir(hash string, o *bytes.Buffer) (err error) {
	// write to output file
	outputFile, err := os.Create(output + "/" + hash + ext)
	if err != nil {
		return fmt.Errorf("error creating output file: %w", err)
	}
	defer outputFile.Close()

	if _, err = io.Copy(outputFile, o); err != nil {
		return fmt.Errorf("error writing to output file: %w", err)
	}

	return
}

func main() {
	fmt.Println("MERCURY SETUP DEPLOYER 4")

	stagingFiles, err := os.ReadDir("staging")
	if err != nil {
		fmt.Println("Error reading staging directory:", err)
		fmt.Println("Please create the staging directory if it doesn't exist and place your files in it, or run this script from a different directory.")
		os.Exit(1)
	}
	if len(stagingFiles) == 0 {
		fmt.Println("Staging directory is empty. Please place your files in the staging directory, or run this script from a different directory.")
		os.Exit(1)
	}

	fmt.Println("Staging directory contains files.")

	// create output directory if it doesn't exist
	if _, err := os.Stat(output); os.IsNotExist(err) {
		if err = os.Mkdir(output, 0o755); err != nil {
			fmt.Println("Error creating output directory:", err)
			os.Exit(1)
		}
	}

	fmt.Println("Output directory is ready.")

	// copy launcher to output directory
	if _, err := os.Stat(launcher); os.IsNotExist(err) {
		fmt.Println("Launcher not found in staging directory. Please place the launcher in the staging directory or run this script from a different directory.")
		os.Exit(1)
	}

	src, err := os.Open(launcher)
	if err != nil {
		fmt.Println("Error opening launcher file:", err)
		os.Exit(1)
	}
	defer src.Close()

	dst, err := os.Create(output + "/" + name + "PlayerLauncher.exe")
	if err != nil {
		fmt.Println("Error creating launcher file in output directory:", err)
		os.Exit(1)
	}
	defer dst.Close()

	if _, err := io.Copy(dst, src); err != nil {
		fmt.Println("Error copying launcher file:", err)
		os.Exit(1)
	}

	fmt.Println("Launcher copied to output directory.")

	start := time.Now()

	o := &bytes.Buffer{}
	id, err := compressStagingDir(o)
	if err != nil {
		fmt.Println("Error compressing staging directory:", err)
		os.Exit(1)
	}

	fmt.Printf("Staging directory compressed to %s in %s\n", ext, time.Since(start))
	start = time.Now()

	// zip staging files to output directory
	if err := writeStagingDir(id, o); err != nil {
		fmt.Println("Error compressing staging files:", err)
		os.Exit(1)
	}

	fmt.Printf("Staging files written to output directory in %s\n", time.Since(start))
}
