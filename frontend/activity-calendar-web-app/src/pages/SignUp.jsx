import React, { useState, useContext } from "react";
import { AuthContext } from "../context/AuthContext";

const SignUp = () => {
    const { register } = useContext(AuthContext);
    const [credentials, setCredentials] = useState({ username: "", password: "" });

    const handleSubmit = async (e) => {
        e.preventDefault();
        try {
            await register(credentials);
            alert("Registration successful! Please log in.");
        } catch (error) {
            console.error("Registration failed:", error);
            alert("Registration failed. Please try again.");
        }
    };

    return (
        <div className="min-h-screen flex items-center justify-center bg-gray-100">
            <div className="w-96 bg-white p-6 rounded shadow-lg">
                <h1 className="text-2xl font-bold mb-4">Sign Up</h1>
                <form onSubmit={handleSubmit}>
                    <div className="mb-4">
                        <label className="block font-semibold mb-1">Username</label>
                        <input
                            type="text"
                            className="w-full p-2 border rounded"
                            value={credentials.username}
                            onChange={(e) => setCredentials({ ...credentials, username: e.target.value })}
                        />
                    </div>
                    <div className="mb-4">
                        <label className="block font-semibold mb-1">Password</label>
                        <input
                            type="password"
                            className="w-full p-2 border rounded"
                            value={credentials.password}
                            onChange={(e) => setCredentials({ ...credentials, password: e.target.value })}
                        />
                    </div>
                    <button
                        type="submit"
                        className="bg-green-500 text-white px-4 py-2 rounded hover:bg-green-600"
                    >
                        Sign Up
                    </button>
                </form>
            </div>
        </div>
    );
};

export default SignUp;