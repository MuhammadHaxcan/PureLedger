import React, { useRef, useState, useEffect } from 'react';

const SignatureCanvas = ({ onSave, onCancel, initialImage = null, initialName = '', width = 400, height = 200 }) => {
    const canvasRef = useRef(null);
    const [isDrawing, setIsDrawing] = useState(false);
    const [hasDrawn, setHasDrawn] = useState(false);
    const [signatureName, setSignatureName] = useState(initialName || '');
    const [error, setError] = useState('');

    useEffect(() => {
        const canvas = canvasRef.current;
        const ctx = canvas.getContext('2d');
        
        // Set white background
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        
        // Load initial image if provided
        if (initialImage) {
            const img = new Image();
            img.onload = () => {
                ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
                setHasDrawn(true);
            };
            img.src = initialImage;
        }
        
        // Set drawing style
        ctx.strokeStyle = '#1a365d';
        ctx.lineWidth = 2;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
    }, [initialImage]);

    // Adopt a changed initialName during render instead of syncing it from an effect.
    const [lastInitialName, setLastInitialName] = useState(initialName);
    if (initialName !== lastInitialName) {
        setLastInitialName(initialName);
        if (initialName) setSignatureName(initialName);
    }

    const getCoordinates = (e) => {
        const canvas = canvasRef.current;
        const rect = canvas.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        
        if (e.touches) {
            return {
                x: (e.touches[0].clientX - rect.left) * scaleX,
                y: (e.touches[0].clientY - rect.top) * scaleY
            };
        }
        return {
            x: (e.clientX - rect.left) * scaleX,
            y: (e.clientY - rect.top) * scaleY
        };
    };

    const startDrawing = (e) => {
        e.preventDefault();
        const { x, y } = getCoordinates(e);
        const ctx = canvasRef.current.getContext('2d');
        ctx.beginPath();
        ctx.moveTo(x, y);
        setIsDrawing(true);
        setError(''); // Clear error when user starts drawing
    };

    const draw = (e) => {
        if (!isDrawing) return;
        e.preventDefault();
        const { x, y } = getCoordinates(e);
        const ctx = canvasRef.current.getContext('2d');
        ctx.lineTo(x, y);
        ctx.stroke();
        setHasDrawn(true);
    };

    const stopDrawing = () => {
        setIsDrawing(false);
    };

    const clearCanvas = () => {
        const canvas = canvasRef.current;
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.strokeStyle = '#1a365d';
        ctx.lineWidth = 2;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        setHasDrawn(false);
        setError('');
    };

    const handleSave = () => {
        if (!signatureName.trim()) {
            setError('Please enter a name for this signature');
            return;
        }
        if (!hasDrawn) {
            setError('Please draw a signature first');
            return;
        }
        
        const canvas = canvasRef.current;
        const imageData = canvas.toDataURL('image/png');
        onSave(signatureName.trim(), imageData);
    };

    const handleNameChange = (e) => {
        setSignatureName(e.target.value);
        if (error) setError(''); // Clear error when user types
    };

    return (
        <div className="signature-canvas-container">
            {error && (
                <div className="alert alert-warning d-flex align-items-center gap-2 py-2 mb-3">
                    <i className="fas fa-exclamation-circle"></i>
                    <span className="small">{error}</span>
                </div>
            )}
            
            <div className="mb-3">
                <label className="form-label fw-medium">Signature Name</label>
                <input
                    type="text"
                    className={`form-control ${error && !signatureName.trim() ? 'is-invalid' : ''}`}
                    placeholder="e.g., Manager Signature, Director Signature"
                    value={signatureName}
                    onChange={handleNameChange}
                />
            </div>
            
            <div className="mb-3">
                <label className="form-label fw-medium">Draw Signature</label>
                <div 
                    className={`border rounded position-relative ${error && !hasDrawn ? 'border-warning' : ''}`}
                    style={{ 
                        backgroundColor: '#f8f9fa',
                        touchAction: 'none'
                    }}
                >
                    <canvas
                        ref={canvasRef}
                        width={width}
                        height={height}
                        style={{ 
                            width: '100%', 
                            height: 'auto',
                            cursor: 'crosshair',
                            display: 'block'
                        }}
                        onMouseDown={startDrawing}
                        onMouseMove={draw}
                        onMouseUp={stopDrawing}
                        onMouseLeave={stopDrawing}
                        onTouchStart={startDrawing}
                        onTouchMove={draw}
                        onTouchEnd={stopDrawing}
                    />
                    
                    {!hasDrawn && (
                        <div 
                            className="position-absolute top-50 start-50 translate-middle text-muted"
                            style={{ pointerEvents: 'none' }}
                        >
                            <p className="mb-0 text-center" style={{ opacity: 0.5 }}>
                                Draw your signature here
                            </p>
                        </div>
                    )}
                </div>
            </div>
            <div className="d-flex justify-content-between">
                <button
                    type="button"
                    className="btn btn-outline-secondary d-flex align-items-center gap-2"
                    onClick={clearCanvas}
                >
                    <i className="fas fa-redo me-2"></i>
                    Clear
                </button>
                
                <div className="d-flex gap-2">
                    <button
                        type="button"
                        className="btn btn-secondary"
                        onClick={onCancel}
                    >
                        Cancel
                    </button>
                    <button
                        type="button"
                        className="btn btn-primary d-flex align-items-center gap-2"
                        onClick={handleSave}
                        disabled={!hasDrawn || !signatureName.trim()}
                    >
                        <i className="fas fa-save me-2"></i>
                        Save Signature
                    </button>
                </div>
            </div>
        </div>
    );
};

export default SignatureCanvas;

