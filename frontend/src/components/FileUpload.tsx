import { useCallback, useRef, useState } from 'react'
import type { DragEvent } from 'react'

interface FileUploadProps {
  disabled: boolean
  onSubmit: (files: File[]) => void
}

export function FileUpload({ disabled, onSubmit }: FileUploadProps) {
  const [files, setFiles] = useState<File[]>([])
  const [isDragging, setIsDragging] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  const addFiles = useCallback((incoming: FileList | File[]) => {
    const msgFiles = Array.from(incoming).filter((f) => f.name.toLowerCase().endsWith('.msg'))
    setFiles((prev) => {
      const existingNames = new Set(prev.map((f) => f.name))
      const merged = [...prev]
      for (const file of msgFiles) {
        if (!existingNames.has(file.name)) {
          merged.push(file)
          existingNames.add(file.name)
        }
      }
      return merged
    })
  }, [])

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    setIsDragging(false)
    if (event.dataTransfer.files.length > 0) {
      addFiles(event.dataTransfer.files)
    }
  }

  const removeFile = (name: string) => {
    setFiles((prev) => prev.filter((f) => f.name !== name))
  }

  return (
    <div className="file-upload">
      <div
        className={`dropzone${isDragging ? ' dropzone--active' : ''}`}
        onDragOver={(e) => {
          e.preventDefault()
          setIsDragging(true)
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={handleDrop}
        onClick={() => inputRef.current?.click()}
      >
        <p>Drag &amp; drop .msg files here, or click to browse</p>
        <input
          ref={inputRef}
          type="file"
          accept=".msg"
          multiple
          hidden
          onChange={(e) => e.target.files && addFiles(e.target.files)}
        />
      </div>

      {files.length > 0 && (
        <ul className="file-list">
          {files.map((file) => (
            <li key={file.name}>
              <span>{file.name}</span>
              <button type="button" onClick={() => removeFile(file.name)} aria-label={`Remove ${file.name}`}>
                &times;
              </button>
            </li>
          ))}
        </ul>
      )}

      <button
        type="button"
        className="primary-button"
        disabled={disabled || files.length === 0}
        onClick={() => onSubmit(files)}
      >
        {disabled ? 'Processing…' : `Process ${files.length || ''} file${files.length === 1 ? '' : 's'}`.trim()}
      </button>
    </div>
  )
}
