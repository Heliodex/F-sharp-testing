// Mercury Setup Deployer 3, now in Go
// goroutines make it run fast as FUCK

package main

import (
	"archive/tar"
	"archive/zip"
	"bytes"
	"compress/gzip"
	"fmt"
	"io"
	"os"
	"sync"
	"time"

	"github.com/andybalholm/brotli"
)

const (
	name       = "Mercury"
	input      = "./staging"
	output     = "./setup"
	launcher   = input + "/" + name + "PlayerLauncher.exe"
	outputTest = output + "/" + name + "Setup"
)

func tarStagingDir(o *bytes.Buffer) (ext string, err error) {
	ext = ".tar"

	w := tar.NewWriter(o)
	defer w.Close()

	return ext, w.AddFS(os.DirFS(input))
}

func zipStagingDir(o *bytes.Buffer) (ext string, err error) {
	ext = ".zip"

	w := zip.NewWriter(o)
	defer w.Close()

	return ext, w.AddFS(os.DirFS(input))
}

func tarGzStagingDir(o *bytes.Buffer) (ext string, err error) {
	ext = ".tar.gz"

	gz := gzip.NewWriter(o)
	defer gz.Close()

	w := tar.NewWriter(gz)
	defer w.Close()

	return ext, w.AddFS(os.DirFS(input))
}

func brStagingDir(o *bytes.Buffer) (ext string, err error) {
	ext = ".tar.br"

	br := brotli.NewWriter(o)
	defer br.Close()

	w := tar.NewWriter(br)
	defer w.Close()

	return ext, w.AddFS(os.DirFS(input))
}

func shartStagingDir(o *bytes.Buffer) (ext string, err error) {
	ext = ".shart"

	t := &bytes.Buffer{}
	w := tar.NewWriter(t)
	defer w.Close()

	if err = w.AddFS(os.DirFS(input)); err != nil {
		return "", fmt.Errorf("error adding files to tar: %w", err)
	}

	bs := t.Bytes()

	// split tar file into chunks
	const chunks = 12
	chunkSize := t.Len()/chunks + 1 // 1/4 of the file size

	writers := make([]*bytes.Buffer, chunks)

	var wg sync.WaitGroup
	wg.Add(chunks)

	for i := range writers {
		writers[i] = &bytes.Buffer{}

		start := i * chunkSize
		end := min(start+chunkSize, t.Len())

		go func(i int, start, end int) {
			fmt.Printf("Compressing chunk %d: %d-%d\n", i, start, end)
			defer wg.Done()
			br := brotli.NewWriter(writers[i])
			defer br.Close()
			br.Write(bs[start:end])
		}(i, start, end)
	}

	wg.Wait()

	// write all chunks to the output buffer
	for i, writer := range writers {
		if _, err := o.Write(writer.Bytes()); err != nil {
			return "", fmt.Errorf("error writing chunk %d: %w", i, err)
		}
	}

	return
}

func writeStagingDir(o *bytes.Buffer, ext string) (err error) {
	// write to output file
	outputFile, err := os.Create(outputTest + ext)
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
	ext, err := tarGzStagingDir(o)
	if err != nil {
		fmt.Println("Error compressing staging directory:", err)
		os.Exit(1)
	}

	fmt.Printf("Staging directory compressed to %s in %s\n", ext, time.Since(start))
	start = time.Now()

	// zip staging files to output directory
	if err := writeStagingDir(o, ext); err != nil {
		fmt.Println("Error compressing staging files:", err)
		os.Exit(1)
	}

	fmt.Printf("Staging files written to output directory in %s\n", time.Since(start))
}
